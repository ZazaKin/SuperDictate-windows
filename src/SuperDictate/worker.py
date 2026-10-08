"""SuperDictate Local Speech Worker (faster-whisper)

Runs as a background worker process for SuperDictate.
Loads the model once into memory, then listens on stdin for transcription requests.
Accelerated by NVIDIA CUDA GPU when available, with automatic CPU fallback.
Bilingual English/Russian detection and transcription.
Audio and text never leave this machine.
"""
import argparse
import fnmatch
import glob
import json
import os
import sys
import numpy as np

# Force UTF-8 stream encoding on Windows to support Cyrillic characters flawlessly
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if hasattr(sys.stdin, 'reconfigure'):
    sys.stdin.reconfigure(encoding='utf-8', errors='replace')

# Add NVIDIA CUDA runtime DLL directories if available on Windows
if sys.platform == "win32":
    try:
        site_packages = os.path.dirname(os.path.dirname(np.__file__))
        for nvidia_bin in glob.glob(os.path.join(site_packages, "nvidia", "*", "bin")):
            if os.path.isdir(nvidia_bin):
                try:
                    os.add_dll_directory(nvidia_bin)
                except Exception:
                    pass
                os.environ["PATH"] = nvidia_bin + os.pathsep + os.environ.get("PATH", "")
    except Exception:
        pass

def emit(**data):
    sys.stdout.write(json.dumps(data, ensure_ascii=False) + "\n")
    sys.stdout.flush()

# The files faster-whisper needs from a CTranslate2 Whisper repository.
MODEL_FILES = ["config.json", "preprocessor_config.json", "model.bin", "tokenizer.json", "vocabulary.*"]


def download(repo, output, revision=None):
    """Fetch a model the user asked for in Settings, at the pinned commit. Prints
    {"total": bytes}, then {"done": true} or {"error": "..."}. The app follows
    progress by the folder size."""
    try:
        from huggingface_hub import HfApi, snapshot_download

        info = HfApi().model_info(repo, revision=revision, files_metadata=True)
        total = sum(
            (sibling.size or 0)
            for sibling in info.siblings
            if any(fnmatch.fnmatch(sibling.rfilename, pattern) for pattern in MODEL_FILES)
        )
        emit(total=total)
        snapshot_download(repo, revision=revision, local_dir=output, allow_patterns=MODEL_FILES)
        emit(done=True)
    except Exception as e:
        emit(error=f"{type(e).__name__}: {e}")
        sys.exit(1)


def main():
    parser = argparse.ArgumentParser(description="SuperDictate speech worker")
    parser.add_argument("--model", type=str, help="Path to model directory or model name")
    parser.add_argument("--device", type=str, default="auto", help="Device (auto, cuda, cpu)")
    parser.add_argument("--compute-type", type=str, default="auto", help="Compute type (auto, float16, int8)")
    parser.add_argument("--download", type=str, help="Hugging Face repository to download into --output, then exit")
    parser.add_argument("--output", type=str, help="Target folder for --download")
    parser.add_argument("--revision", type=str, help="Repository commit for --download")
    args = parser.parse_args()

    if args.download:
        if not args.output:
            parser.error("--download needs --output")
        download(args.download, args.output, args.revision)
        return

    if not args.model:
        parser.error("--model is required")

    # Auto-repair missing preprocessor_config.json for large models
    if os.path.isdir(args.model) and not os.path.isfile(os.path.join(args.model, "preprocessor_config.json")):
        config_path = os.path.join(args.model, "config.json")
        if os.path.isfile(config_path):
            try:
                with open(config_path, "r", encoding="utf-8") as f:
                    cfg = json.load(f)
                n_mels = 128 if "large-v3" in args.model.lower() or cfg.get("num_mel_bins") == 128 else 80
                prep_cfg = {
                    "feature_extractor_type": "WhisperFeatureExtractor",
                    "feature_size": n_mels,
                    "hop_length": 160,
                    "chunk_length": 30,
                    "n_fft": 400,
                    "n_samples": 480000,
                    "nb_max_frames": 3000,
                    "sampling_rate": 16000
                }
                with open(os.path.join(args.model, "preprocessor_config.json"), "w", encoding="utf-8") as f:
                    json.dump(prep_cfg, f, indent=2)
            except Exception:
                pass

    device = args.device
    compute_type = args.compute_type

    if device == "auto":
        try:
            import ctranslate2
            if ctranslate2.get_cuda_device_count() > 0:
                device = "cuda"
                compute_type = "float16" if compute_type == "auto" else compute_type
            else:
                device = "cpu"
                compute_type = "int8" if compute_type == "auto" else compute_type
        except Exception:
            device = "cpu"
            compute_type = "int8" if compute_type == "auto" else compute_type
    elif compute_type == "auto":
        compute_type = "float16" if device == "cuda" else "int8"

    try:
        from faster_whisper import WhisperModel
        try:
            model = WhisperModel(
                args.model,
                device=device,
                compute_type=compute_type,
                cpu_threads=min(4, os.cpu_count() or 4),
            )
            # Dry-run validation to ensure CUDA cuBLAS/cuDNN runtime is functional
            if device == "cuda":
                dummy_audio = np.zeros(16000, dtype=np.float32)
                model.detect_language(dummy_audio)
        except Exception as gpu_err:
            if device == "cuda":
                sys.stderr.write(f"CUDA validation failed ({gpu_err}), falling back to CPU int8\n")
                device = "cpu"
                compute_type = "int8"
                model = WhisperModel(
                    args.model,
                    device=device,
                    compute_type=compute_type,
                    cpu_threads=min(4, os.cpu_count() or 4),
                )
            else:
                raise gpu_err
    except Exception as e:
        emit(error=f"Failed to load speech model: {e}", ready=False)
        sys.exit(1)

    emit(ready=True, device=device, compute_type=compute_type)

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            req = json.loads(line)
            audio_path = req.get("path")
            lang_hint = req.get("language")
            selected_langs = req.get("selected_languages") or ["en", "ru", "de", "pl"]
            # A draft feeds the live preview while the user is still talking: it must be
            # quick, and it must stay empty rather than guess at noise.
            draft = bool(req.get("draft"))

            if not audio_path or not os.path.exists(audio_path):
                emit(text="", error="Audio file not found")
                continue

            if os.path.getsize(audio_path) == 0:
                emit(text="")
                continue

            # Load audio: supports raw float32 (.f32) or standard wav
            if audio_path.endswith(".f32"):
                audio = np.fromfile(audio_path, dtype=np.float32)
            else:
                audio = audio_path

            # A short, well punctuated sentence in the language nudges Whisper toward
            # clean punctuation. A language without one gets no prompt: an English
            # prompt would pull other languages toward English.
            prompts = {
                "ru": "Привет, как дела? Запятая, точка.",
                "en": "Hello, how are you? Punctuation, period.",
                "de": "Hallo, wie geht's? Komma, Punkt.",
                "pl": "Cześć, jak się masz? Przecinek, kropka.",
                "es": "Hola, ¿cómo estás? Muy bien, gracias.",
                "fr": "Bonjour, comment ça va ? Très bien, merci.",
                "it": "Ciao, come stai? Bene, grazie.",
                "pt": "Olá, tudo bem? Sim, obrigado.",
                "nl": "Hallo, hoe gaat het? Goed, dank je.",
                "uk": "Привіт, як справи? Добре, дякую.",
                "cs": "Ahoj, jak se máš? Dobře, děkuji.",
                "sv": "Hej, hur mår du? Bra, tack.",
                "el": "Γεια σου, τι κάνεις; Καλά, ευχαριστώ.",
                "ro": "Bună, ce mai faci? Bine, mulțumesc.",
                "hu": "Szia, hogy vagy? Jól, köszönöm.",
                "bg": "Здравей, как си? Добре, благодаря.",
                "da": "Hej, hvordan har du det? Godt, tak.",
                "fi": "Hei, mitä kuuluu? Hyvää, kiitos.",
                "sk": "Ahoj, ako sa máš? Dobre, ďakujem.",
                "hr": "Bok, kako si? Dobro, hvala.",
                "lt": "Labas, kaip sekasi? Gerai, ačiū.",
                "sl": "Živjo, kako si? Dobro, hvala.",
                "lv": "Sveiki, kā jums iet? Labi, paldies.",
                "et": "Tere, kuidas läheb? Hästi, aitäh.",
                "mt": "Bongu, kif inti? Tajjeb, grazzi.",
            }

            # Language selection logic:
            if lang_hint in ("auto", "", None):
                # Detect language probabilities across all supported languages
                _, _, all_probs = model.detect_language(audio)
                probs = dict(all_probs)

                # Close relatives Whisper confuses lend their weight to a selected
                # language, unless the user selected the relative too.
                relatives = {
                    "ru": (("uk", 0.8), ("be", 0.8)),
                    "pl": (("cs", 0.7), ("sk", 0.7)),
                    "de": (("nl", 0.6), ("lb", 0.5)),
                }
                candidates = {}
                for lang in selected_langs:
                    score = probs.get(lang, 0.0)
                    for relative, weight in relatives.get(lang, ()):
                        if relative not in selected_langs:
                            score += probs.get(relative, 0.0) * weight
                    candidates[lang] = score

                top_lang, top_prob = all_probs[0]

                # Whisper quirk: Slavic speech is often taken for Portuguese, Spanish or
                # Catalan. Only when the user doesn't speak that language does it count
                # for Russian or Polish.
                if top_lang in ("pt", "es", "ca") and top_lang not in selected_langs:
                    slavic = [lang for lang in ("ru", "pl") if lang in candidates]
                    if slavic:
                        best = max(slavic, key=candidates.get)
                        candidates[best] += top_prob * 0.75

                chosen_lang = max(candidates, key=candidates.get) if candidates else top_lang
            else:
                chosen_lang = lang_hint

            prompt = prompts.get(chosen_lang)

            segments, info = model.transcribe(
                audio,
                language=chosen_lang,
                initial_prompt=prompt,
                beam_size=1,
                best_of=1,
                temperature=0.0,
                condition_on_previous_text=False,
                vad_filter=True,
                without_timestamps=draft,
            )

            pieces = [s.text.strip() for s in segments if s.text.strip()]
            text = " ".join(pieces)

            # Robust fallback: if VAD dropped everything, but audio has audible signal, retry without VAD.
            # Never for a draft: without VAD, Whisper tends to invent words for room noise.
            if not text and not draft and isinstance(audio, np.ndarray) and len(audio) > 4000:
                max_amp = float(np.max(np.abs(audio)))
                if max_amp > 0.008:
                    segments, _ = model.transcribe(
                        audio,
                        language=chosen_lang,
                        initial_prompt=prompt,
                        beam_size=1,
                        best_of=1,
                        temperature=0.0,
                        condition_on_previous_text=False,
                        vad_filter=False,
                    )
                    pieces = [s.text.strip() for s in segments if s.text.strip()]
                    text = " ".join(pieces)

            emit(text=text, language=chosen_lang)

        except Exception as e:
            sys.stderr.write(f"Worker transcription exception: {e}\n")
            emit(error=str(e), text="")

if __name__ == "__main__":
    main()
