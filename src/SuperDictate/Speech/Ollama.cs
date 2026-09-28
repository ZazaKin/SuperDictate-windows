using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperDictate.Storage;

namespace SuperDictate.Speech;

/// <summary>
/// Local LLM models for AI cleanup, through Ollama's API. LM Studio and other
/// OpenAI-compatible servers have no download API; their models are managed there.
/// </summary>
public static class Ollama
{
    public enum State
    {
        Installed,
        Missing,
        NotRunning,
        NotOllama,
    }

    internal static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public const string DownloadPage = "https://ollama.com/download";

    /// <summary>The Ollama server behind an OpenAI-style endpoint such as http://localhost:11434/v1.</summary>
    public static string ServerUrl(string endpoint)
    {
        var url = endpoint.Trim().TrimEnd('/');
        return url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? url[..^3] : url;
    }

    /// <summary>To Ollama, "llama3.2" means "llama3.2:latest".</summary>
    public static string FullName(string model) => model.Contains(':') ? model.Trim() : model.Trim() + ":latest";

    public static async Task<State> CheckAsync(string endpoint, string model, CancellationToken token)
    {
        try
        {
            using var quick = CancellationTokenSource.CreateLinkedTokenSource(token);
            quick.CancelAfter(TimeSpan.FromSeconds(2));
            using var response = await Http.GetAsync(ServerUrl(endpoint) + "/api/tags", quick.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return State.NotOllama;

            using var tags = JsonDocument.Parse(await response.Content.ReadAsStringAsync(quick.Token).ConfigureAwait(false));
            if (!tags.RootElement.TryGetProperty("models", out var models)) return State.NotOllama;

            var wanted = FullName(model);
            return models.EnumerateArray().Any(entry =>
                entry.TryGetProperty("name", out var name) && string.Equals(name.GetString(), wanted, StringComparison.OrdinalIgnoreCase))
                ? State.Installed
                : State.Missing;
        }
        catch (HttpRequestException)
        {
            return State.NotRunning;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return State.NotRunning;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or UriFormatException)
        {
            return State.NotOllama;
        }
    }
}

/// <summary>
/// One Ollama model download at a time. Progress adds up the layers Ollama
/// reports; it lives outside the settings window, like model downloads.
/// </summary>
public sealed class OllamaPull
{
    private readonly CancellationTokenSource _cancel = new();
    private readonly Dictionary<string, (long Done, long Total)> _layers = new();
    private volatile string _status = "Starting…";

    private OllamaPull(string endpoint, string model)
    {
        Endpoint = endpoint;
        Model = Ollama.FullName(model);
        Current = this;
        Finished = Task.Run(PullAsync);
    }

    public static OllamaPull? Current { get; private set; }

    public string Endpoint { get; }

    public string Model { get; }

    public string Status => _status;

    public (long Done, long Total) Bytes
    {
        get
        {
            lock (_layers)
            {
                return (_layers.Values.Sum(layer => layer.Done), _layers.Values.Sum(layer => layer.Total));
            }
        }
    }

    public bool Cancelled => _cancel.IsCancellationRequested;

    /// <summary>Null when the model is in Ollama; otherwise what went wrong.</summary>
    public Task<string?> Finished { get; }

    public static OllamaPull Start(string endpoint, string model)
    {
        if (Current is { } running) throw new InvalidOperationException($"{running.Model} is still downloading.");
        return new OllamaPull(endpoint, model);
    }

    public void Cancel() => _cancel.Cancel();

    private async Task<string?> PullAsync()
    {
        try
        {
            // "model" for current Ollama, "name" for older versions.
            var body = JsonSerializer.Serialize(new { model = Model, name = Model, stream = true });
            using var request = new HttpRequestMessage(HttpMethod.Post, Ollama.ServerUrl(Endpoint) + "/api/pull")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            using var response = await Ollama.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _cancel.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(_cancel.Token).ConfigureAwait(false));
            string? error = null;
            var success = false;
            while (await reader.ReadLineAsync(_cancel.Token).ConfigureAwait(false) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var message = JsonDocument.Parse(line);
                var root = message.RootElement;
                if (root.TryGetProperty("error", out var failure)) error = failure.GetString();
                if (root.TryGetProperty("status", out var status))
                {
                    var text = status.GetString() ?? "";
                    success |= text == "success";
                    _status = text.StartsWith("pulling ", StringComparison.Ordinal) ? "Downloading…" : Capitalize(text);
                }

                if (root.TryGetProperty("digest", out var digest) && root.TryGetProperty("total", out var total))
                {
                    var done = root.TryGetProperty("completed", out var completed) ? completed.GetInt64() : 0;
                    lock (_layers)
                    {
                        _layers[digest.GetString() ?? ""] = (done, total.GetInt64());
                    }
                }
            }

            if (success) AppLogger.Info($"Ollama downloaded {Model}");
            return success ? null : error ?? "Ollama stopped before the model was complete.";
        }
        catch (OperationCanceledException)
        {
            return "Cancelled";
        }
        catch (Exception error) when (error is HttpRequestException or IOException or JsonException)
        {
            AppLogger.Error($"Ollama download of {Model} failed", error);
            return error.Message;
        }
        finally
        {
            Current = null;
        }
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
