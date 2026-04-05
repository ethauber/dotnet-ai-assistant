using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Exceptions;
using Core.Services;

namespace Infrastructure.Services;

public sealed class RepoAssistantService : IRepoAssistantService
{
    private static readonly ConcurrentDictionary<string, CachedPromptyDocument> PromptCache = new();
    private static readonly SemaphoreSlim PromptCacheLock = new(1, 1);

    private readonly HttpClient _httpClient;
    private readonly string _promptyPath;

    public RepoAssistantService(HttpClient httpClient, string promptyPath)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _promptyPath = promptyPath ?? throw new ArgumentNullException(nameof(promptyPath));
    }

    public async Task<string> RunAsync(
        string userGoal,
        string? fileContext = null,
        string? projectArea = null,
        CancellationToken cancellationToken = default
    )
    {
        var prompt = await LoadPromptAsync(cancellationToken);

        using var request = BuildRequest(prompt, userGoal, fileContext, projectArea);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new UpstreamServiceException(
                "The configured model endpoint returned a non-success response.",
                (int)response.StatusCode
            );
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (
                document.RootElement.TryGetProperty("choices", out var choices)
                && choices.GetArrayLength() > 0
            )
            {
                var firstChoice = choices[0];
                if (
                    firstChoice.TryGetProperty("message", out var message)
                    && message.TryGetProperty("content", out var content)
                )
                {
                    return content.GetString() ?? string.Empty;
                }

                if (firstChoice.TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? string.Empty;
                }
            }
        }
        catch (JsonException exception)
        {
            throw new UpstreamServiceException(
                "The configured model endpoint returned an unexpected response payload.",
                innerException: exception
            );
        }

        throw new UpstreamServiceException(
            "The configured model endpoint returned no assistant content."
        );
    }

    private HttpRequestMessage BuildRequest(
        PromptyDocument prompt,
        string userGoal,
        string? fileContext,
        string? projectArea
    )
    {
        var renderedPrompt = prompt.RenderBody(userGoal, fileContext, projectArea);
        var body = JsonSerializer.Serialize(
            new
            {
                model = prompt.Model,
                messages = new[]
                {
                    new { role = "system", content = renderedPrompt },
                    new
                    {
                        role = "user",
                        content = BuildUserMessage(userGoal, fileContext, projectArea),
                    },
                },
                temperature = prompt.Temperature,
                max_tokens = prompt.MaxTokens,
            }
        );

        var request = new HttpRequestMessage(HttpMethod.Post, prompt.Endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        if (!string.IsNullOrWhiteSpace(prompt.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", prompt.ApiKey);
        }

        return request;
    }

    private async Task<PromptyDocument> LoadPromptAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_promptyPath))
        {
            throw new PromptTemplateNotFoundException(_promptyPath);
        }

        var lastWriteTimeUtc = File.GetLastWriteTimeUtc(_promptyPath);
        if (
            PromptCache.TryGetValue(_promptyPath, out var cachedPrompt)
            && cachedPrompt.LastWriteTimeUtc == lastWriteTimeUtc
        )
        {
            return cachedPrompt.Document;
        }

        await PromptCacheLock.WaitAsync(cancellationToken);
        try
        {
            lastWriteTimeUtc = File.GetLastWriteTimeUtc(_promptyPath);
            if (
                PromptCache.TryGetValue(_promptyPath, out cachedPrompt)
                && cachedPrompt.LastWriteTimeUtc == lastWriteTimeUtc
            )
            {
                return cachedPrompt.Document;
            }

            var rawPrompt = await File.ReadAllTextAsync(_promptyPath, cancellationToken);
            var prompt = PromptyDocument.Parse(rawPrompt);
            PromptCache[_promptyPath] = new CachedPromptyDocument(prompt, lastWriteTimeUtc);

            return prompt;
        }
        finally
        {
            PromptCacheLock.Release();
        }
    }

    private static string BuildUserMessage(
        string userGoal,
        string? fileContext,
        string? projectArea
    )
    {
        var builder = new StringBuilder();
        builder.AppendLine($"User goal: {userGoal}");

        if (!string.IsNullOrWhiteSpace(projectArea))
        {
            builder.AppendLine($"Project area: {projectArea}");
        }

        if (!string.IsNullOrWhiteSpace(fileContext))
        {
            builder.AppendLine("File context:");
            builder.AppendLine(fileContext);
        }

        return builder.ToString();
    }

    private sealed record PromptyDocument(
        string Endpoint,
        string ApiKey,
        string Model,
        double Temperature,
        int MaxTokens,
        string Body
    )
    {
        public static PromptyDocument Parse(string rawPrompt)
        {
            var parts = Regex.Split(rawPrompt, "^---\\r?$", RegexOptions.Multiline);
            var yaml = parts.Length >= 3 ? parts[1] : string.Empty;
            var body =
                parts.Length >= 3 ? string.Join("\n---\n", parts[2..]).Trim() : rawPrompt.Trim();

            var baseUrl = ExtractYamlValue(yaml, "base_url");
            var apiKey = ExtractYamlValue(yaml, "api_key");
            var model = ExtractYamlValue(yaml, "model");
            var temperature = ParseDouble(ExtractYamlValue(yaml, "temperature"), 0.2);
            var maxTokens = ParseInt(ExtractYamlValue(yaml, "max_tokens"), 800);

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = "http://localhost:11434/v1";
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-4o-mini";
            }

            return new PromptyDocument(
                Endpoint: baseUrl.TrimEnd('/') + "/chat/completions",
                ApiKey: apiKey,
                Model: model,
                Temperature: temperature,
                MaxTokens: maxTokens,
                Body: body
            );
        }

        public string RenderBody(string userGoal, string? fileContext, string? projectArea)
        {
            return Body.Replace("{user_goal}", userGoal, StringComparison.Ordinal)
                .Replace("{file_context}", fileContext ?? string.Empty, StringComparison.Ordinal)
                .Replace("{project_area}", projectArea ?? string.Empty, StringComparison.Ordinal);
        }

        private static string ExtractYamlValue(string yaml, string key)
        {
            var match = Regex.Match(yaml, $"{Regex.Escape(key)}:\\s*(.+)$", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim().Trim('"') : string.Empty;
        }

        private static double ParseDouble(string value, double fallback)
        {
            return double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var parsed
            )
                ? parsed
                : fallback;
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }
    }

    private sealed record CachedPromptyDocument(
        PromptyDocument Document,
        DateTime LastWriteTimeUtc
    );
}
