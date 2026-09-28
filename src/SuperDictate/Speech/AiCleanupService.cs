using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SuperDictate.Storage;

namespace SuperDictate.Speech;

/// <summary>
/// Multilingual speech cleanup engine supporting:
/// 1. Built-in Local Smart Cleaner: Zero-latency, zero-resource local rule/NLP engine.
/// 2. Local LLM: Connects to local Ollama / LM Studio on localhost without sending data off-device.
/// 3. Cloud LLM: Optional Groq / OpenAI compatible API.
/// Always falls back gracefully to ensure dictation never fails or delays.
/// </summary>
public static class AiCleanupService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public static async Task<string> CleanAsync(string rawText, Settings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawText) || !settings.AiCleanupEnabled)
        {
            return rawText;
        }

        var mode = settings.AiCleanupMode?.ToLowerInvariant() ?? "local_smart";

        try
        {
            if (mode == "local_llm")
            {
                var result = await CleanLocalLlmAsync(rawText, settings, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(result)) return result;
            }
            else if (mode == "cloud")
            {
                var result = await CleanCloudLlmAsync(rawText, settings, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(result)) return result;
            }
        }
        catch
        {
            // Fall through to local smart cleaner
        }

        // Default or fallback: Built-in Local Smart Cleaner
        return CleanLocalSmart(rawText, settings);
    }

    /// <summary>
    /// Instant local NLP cleanup engine: removes stuttering, replaces spoken punctuation,
    /// strips filler words in Russian, English, German, and Polish, and repairs capitalization/spacing.
    /// </summary>
    public static string CleanLocalSmart(string rawText, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

        var text = rawText.Trim();

        // 1. Spoken punctuation word replacements
        if (settings.AiFormatPunctuation)
        {
            text = ReplaceSpokenPunctuation(text);
        }

        // 2. Remove accidental stuttered / repeated words (e.g. "I I went", "это это")
        if (settings.AiRemoveDuplicates)
        {
            text = Regex.Replace(text, @"\b(\p{L}+)[\s,-]+(\1)\b", "$1", RegexOptions.IgnoreCase);
        }

        // 3. Remove verbal fillers across supported languages
        if (settings.AiRemoveFillers)
        {
            text = StripVerbalFillers(text, settings.CustomFillerWords);
        }

        // 4. Normalize spacing around punctuation
        text = Regex.Replace(text, @"\s+([,\.!\?:;])", "$1"); // No space before punctuation
        text = Regex.Replace(text, @"([,\.!\?:;])(?!\s|$|\d)", "$1 "); // Space after punctuation
        text = Regex.Replace(text, @"\s{2,}", " "); // Collapse multiple spaces

        // 5. Capitalize sentences
        if (settings.AiFormatPunctuation && text.Length > 0)
        {
            text = CapitalizeSentences(text);
        }

        return text.Trim();
    }

    private static string ReplaceSpokenPunctuation(string text)
    {
        // Spoken punctuation dictionary (case-insensitive)
        var replacements = new (string pattern, string replacement)[]
        {
            // Russian
            (@"\s+\bточка\b", "."),
            (@"\s+\bзапятая\b", ","),
            (@"\s+\bвопросительный знак\b", "?"),
            (@"\s+\bвосклицательный знак\b", "!"),
            (@"\s+\bдвоеточие\b", ":"),
            (@"\s+\bновая строка\b", "\n"),
            // English
            (@"\s+\bperiod\b", "."),
            (@"\s+\bcomma\b", ","),
            (@"\s+\bquestion mark\b", "?"),
            (@"\s+\bexclamation mark\b", "!"),
            (@"\s+\bcolon\b", ":"),
            (@"\s+\bnew line\b", "\n"),
            // German
            (@"\s+\bpunkt\b", "."),
            (@"\s+\bkomma\b", ","),
            (@"\s+\bfragezeichen\b", "?"),
            (@"\s+\bausrufezeichen\b", "!"),
            (@"\s+\bdoppelpunkt\b", ":"),
            // Polish
            (@"\s+\bkropka\b", "."),
            (@"\s+\bprzecinek\b", ","),
            (@"\s+\bznak zapytania\b", "?"),
            (@"\s+\bwykrzyknik\b", "!"),
            (@"\s+\bdwukropek\b", ":"),
        };

        foreach (var (pattern, rep) in replacements)
        {
            text = Regex.Replace(text, pattern, rep, RegexOptions.IgnoreCase);
        }

        return text;
    }

    private static string StripVerbalFillers(string text, string? customWords)
    {
        // Multi-language filler words list
        var fillers = new List<string>
        {
            // Russian
            @"эээ", @"ммм", @"ну", @"типа", @"короче", @"как бы", @"в общем", @"так сказать", @"значит",
            // English
            @"um", @"uh", @"like", @"you know", @"actually", @"basically", @"sort of", @"kind of",
            // German
            @"äh", @"ähm", @"halt", @"sozusagen", @"quasi", @"weißt du",
            // Polish
            @"yyy", @"eee", @"no", @"znaczy", @"jakby", @"wiesz", @"po prostu"
        };

        if (!string.IsNullOrWhiteSpace(customWords))
        {
            var custom = customWords.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(w => w.Trim())
                                    .Where(w => w.Length > 0);
            fillers.AddRange(custom);
        }

        foreach (var filler in fillers)
        {
            var pattern = $@"(^|[\s,;]+)\b{Regex.Escape(filler)}\b([\s,;]+|$)";
            text = Regex.Replace(text, pattern, m =>
            {
                var prefix = m.Groups[1].Value.Contains(',') ? "," : " ";
                return prefix;
            }, RegexOptions.IgnoreCase);
        }

        // Clean up any double commas or isolated punctuation left over
        text = Regex.Replace(text, @"^[,\.\s]+", ""); // Leading punctuation
        text = Regex.Replace(text, @",\s*,+", ",");

        return text;
    }

    private static string CapitalizeSentences(string text)
    {
        var chars = text.ToCharArray();
        var capitalizeNext = true;

        for (var i = 0; i < chars.Length; i++)
        {
            if (capitalizeNext && char.IsLetter(chars[i]))
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
                capitalizeNext = false;
            }
            else if (chars[i] is '.' or '?' or '!' or '\n')
            {
                capitalizeNext = true;
            }
        }

        return new string(chars);
    }

    private static async Task<string?> CleanLocalLlmAsync(string rawText, Settings settings, CancellationToken cancellationToken)
    {
        var endpoint = (settings.LocalLlmEndpoint ?? "http://localhost:11434/v1").TrimEnd('/') + "/chat/completions";
        var model = string.IsNullOrWhiteSpace(settings.LocalLlmModel) ? "llama3.2:1b" : settings.LocalLlmModel;

        return await SendLlmChatRequestAsync(endpoint, model, string.Empty, rawText, settings.AiTimeoutSeconds, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> CleanCloudLlmAsync(string rawText, Settings settings, CancellationToken cancellationToken)
    {
        var endpoint = settings.AiBaseUrl.TrimEnd('/') + "/chat/completions";
        var storedKey = CredentialStore.Read(CredentialStore.AiCleanupTarget);
        var apiKey = !string.IsNullOrWhiteSpace(storedKey)
                   ? storedKey
                   : Environment.GetEnvironmentVariable("SUPERDICTATE_AI_KEY")
                  ?? Environment.GetEnvironmentVariable("GROQ_API_KEY")
                  ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                  ?? string.Empty;

        return await SendLlmChatRequestAsync(endpoint, settings.AiModel, apiKey, rawText, settings.AiTimeoutSeconds, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> SendLlmChatRequestAsync(string endpoint, string model, string apiKey, string rawText, int timeoutSeconds, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(2, timeoutSeconds)));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var payload = new
        {
            model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "You are a professional multilingual speech cleanup editor. Clean up the transcribed speech into natural, perfectly formatted text. Fix punctuation, capitalization, and grammar. Remove verbal filler words (such as 'um', 'uh', 'like' in English; 'эээ', 'ммм', 'ну', 'типа', 'короче', 'как бы' in Russian; 'äh', 'ähm', 'halt', 'sozusagen' in German; 'yyy', 'eee', 'znaczy', 'jakby' in Polish). Preserve the original language and original meaning exactly. Output ONLY the polished text with no preamble, quotes, or conversational explanations."
                },
                new
                {
                    role = "user",
                    content = rawText
                }
            },
            temperature = 0.2,
            max_tokens = 1024,
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = jsonContent,
        };

        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        var response = await HttpClient.SendAsync(request, linkedCts.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var responseBody = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(responseBody);

        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content))
            {
                return content.GetString()?.Trim();
            }
        }

        return null;
    }
}
