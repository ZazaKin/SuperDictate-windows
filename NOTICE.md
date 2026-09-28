# Third-party notices

SuperDictate for Windows is based on
[Parakey](https://github.com/rcourtman/parakey) by Richard Courtman, licensed
under MIT. Its original copyright notice and license are kept in `LICENSE`.

## Speech models

The installer contains no models. Users download them from Hugging Face in the
app's settings:

- [OpenAI Whisper](https://github.com/openai/whisper): MIT license.
- CTranslate2 builds for faster-whisper:
  [Systran/faster-whisper-small](https://huggingface.co/Systran/faster-whisper-small),
  [-base](https://huggingface.co/Systran/faster-whisper-base),
  [-tiny](https://huggingface.co/Systran/faster-whisper-tiny) and
  [mobiuslabsgmbh/faster-whisper-large-v3-turbo](https://huggingface.co/mobiuslabsgmbh/faster-whisper-large-v3-turbo):
  MIT license.

## Libraries

- [NAudio](https://github.com/naudio/NAudio): MIT, built into the exe.
- Installed on first run: Python (PSF license, from NuGet),
  [faster-whisper](https://github.com/SYSTRAN/faster-whisper) (MIT),
  [CTranslate2](https://github.com/OpenNMT/CTranslate2) (MIT) and their
  dependencies from PyPI, as listed in `src/SuperDictate/runtime-requirements.txt`.
- Optional, for NVIDIA graphics cards: CUDA libraries (cuBLAS, cuDNN, NVRTC)
  under NVIDIA's license, listed in `src/SuperDictate/runtime-requirements-gpu.txt`.
