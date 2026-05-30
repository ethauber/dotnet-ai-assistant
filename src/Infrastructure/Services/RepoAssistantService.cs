using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Exceptions;
using Core.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class RepoAssistantService : IRepoAssistantService
{
    private static readonly ConcurrentDictionary<string, CachedPromptyDocument> PromptCache = new();
    private static readonly SemaphoreSlim PromptCacheLock = new(1, 1);
    private const int MaxThrottleRetries = 2;

    private readonly HttpClient _httpClient;
    private readonly string _promptsDirectory;
    private readonly string? _repoRootPath;
    private readonly ILogger<RepoAssistantService> _logger;

    // Simple single-entry cache: stamp is the max LastWriteTimeUtc of watched files.
    private (DateTime Stamp, string Context) _contextCache;

    public RepoAssistantService(
        HttpClient httpClient,
        string promptsDirectory,
        ILogger<RepoAssistantService> logger,
        string? repoRootPath = null
    )
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _promptsDirectory =
            promptsDirectory ?? throw new ArgumentNullException(nameof(promptsDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repoRootPath = repoRootPath;
    }

    public async Task<PromptRunResult> RunAsync(
        string templateName,
        string userGoal,
        string? fileContext = null,
        string? projectArea = null,
        CancellationToken cancellationToken = default
    )
    {
        // Validate templateName to prevent path traversal attacks.
        if (
            templateName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || templateName.Contains("..", StringComparison.Ordinal)
            || templateName.Contains(Path.DirectorySeparatorChar)
            || templateName.Contains(Path.AltDirectorySeparatorChar)
        )
        {
            throw new ArgumentException("Invalid template name.", nameof(templateName));
        }

        var promptyPath = Path.Combine(_promptsDirectory, templateName + ".prompty");
        var fullPromptyPath = Path.GetFullPath(promptyPath);
        var fullPromptsDir = Path.GetFullPath(_promptsDirectory);
        if (!fullPromptyPath.StartsWith(fullPromptsDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid template name.", nameof(templateName));
        }
        var (prompt, version) = await LoadPromptAsync(promptyPath, cancellationToken);
        var resolvedContext = await ResolveContextAsync(
            templateName,
            fileContext,
            cancellationToken
        );

        for (var attempt = 0; attempt <= MaxThrottleRetries; attempt++)
        {
            using var request = BuildRequest(prompt, userGoal, resolvedContext, projectArea);

            try
            {
                TimeSpan? retryDelay = null;

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (
                    response.StatusCode == HttpStatusCode.TooManyRequests
                    && attempt < MaxThrottleRetries
                )
                {
                    retryDelay = GetRetryDelay(response, attempt);
                }
                else
                {
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        throw new UpstreamServiceException(
                            "The configured model endpoint is currently throttling requests.",
                            (int)response.StatusCode
                        );
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = response.Content is not null
                            ? await response.Content.ReadAsStringAsync(cancellationToken)
                            : string.Empty;
                        _logger.LogWarning(
                            "Upstream model endpoint returned {StatusCode}. Model={Model} Endpoint={Endpoint} Body={ResponseBody}",
                            (int)response.StatusCode,
                            prompt.Model,
                            prompt.Endpoint,
                            errorBody
                        );
                        throw new UpstreamServiceException(
                            $"The configured model endpoint returned {(int)response.StatusCode}.",
                            (int)response.StatusCode
                        );
                    }

                    var responseText = response.Content is not null
                        ? await response.Content.ReadAsStringAsync(cancellationToken)
                        : string.Empty;

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
                                var assistantContent = content.GetString();
                                if (!string.IsNullOrWhiteSpace(assistantContent))
                                {
                                    return new PromptRunResult(assistantContent, version);
                                }
                            }

                            if (firstChoice.TryGetProperty("text", out var text))
                            {
                                var assistantText = text.GetString();
                                if (!string.IsNullOrWhiteSpace(assistantText))
                                {
                                    return new PromptRunResult(assistantText, version);
                                }
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

                if (retryDelay is not null)
                {
                    await Task.Delay(retryDelay.Value, cancellationToken);
                    continue;
                }
            }
            catch (HttpRequestException exception)
            {
                throw new UpstreamServiceException(
                    "The configured model endpoint is currently unavailable.",
                    innerException: exception
                );
            }
            catch (TaskCanceledException exception)
                when (!cancellationToken.IsCancellationRequested)
            {
                throw new UpstreamServiceException(
                    "The configured model endpoint did not respond in time.",
                    innerException: exception
                );
            }
        }

        throw new UpstreamServiceException(
            "The configured model endpoint returned no assistant content."
        );
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is TimeSpan retryAfterDelta)
        {
            return retryAfterDelta;
        }

        if (response.Headers.RetryAfter?.Date is DateTimeOffset retryAfterDate)
        {
            var delay = retryAfterDate - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                return delay;
            }
        }

        return TimeSpan.FromMilliseconds(Math.Min(500 * Math.Pow(2, attempt), 2000));
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
                    new { role = "user", content = BuildUserMessage(userGoal, projectArea) },
                },
                temperature = prompt.Temperature,
                max_tokens = prompt.MaxTokens,
            }
        );

        var request = new HttpRequestMessage(HttpMethod.Post, prompt.Endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        _logger.LogDebug(
            "Sending request to {Endpoint} — model={Model} temperature={Temperature} max_tokens={MaxTokens} body={Body}",
            prompt.Endpoint,
            prompt.Model,
            prompt.Temperature,
            prompt.MaxTokens,
            body
        );

        if (!string.IsNullOrWhiteSpace(prompt.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", prompt.ApiKey);
        }

        return request;
    }

    private static async Task<(PromptyDocument doc, string version)> LoadPromptAsync(
        string promptyPath,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (!File.Exists(promptyPath))
            {
                throw new PromptTemplateNotFoundException(promptyPath);
            }

            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(promptyPath);
            if (
                PromptCache.TryGetValue(promptyPath, out var cachedPrompt)
                && cachedPrompt.LastWriteTimeUtc == lastWriteTimeUtc
            )
            {
                return (cachedPrompt.Document, cachedPrompt.Version);
            }

            await PromptCacheLock.WaitAsync(cancellationToken);
            try
            {
                lastWriteTimeUtc = File.GetLastWriteTimeUtc(promptyPath);
                if (
                    PromptCache.TryGetValue(promptyPath, out cachedPrompt)
                    && cachedPrompt.LastWriteTimeUtc == lastWriteTimeUtc
                )
                {
                    return (cachedPrompt.Document, cachedPrompt.Version);
                }

                var rawPrompt = await File.ReadAllTextAsync(promptyPath, cancellationToken);
                var prompt = PromptyDocument.Parse(rawPrompt);
                var version = ComputeVersion(rawPrompt);
                PromptCache[promptyPath] = new CachedPromptyDocument(
                    prompt,
                    lastWriteTimeUtc,
                    version
                );

                return (prompt, version);
            }
            finally
            {
                PromptCacheLock.Release();
            }
        }
        catch (FileNotFoundException)
        {
            throw new PromptTemplateNotFoundException(promptyPath);
        }
        catch (DirectoryNotFoundException)
        {
            throw new PromptTemplateNotFoundException(promptyPath);
        }
        catch (IOException)
        {
            throw new PromptTemplateNotFoundException(promptyPath);
        }
    }

    private static string ComputeVersion(string rawContent)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawContent));
        return Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
    }

    private async Task<string?> ResolveContextAsync(
        string templateName,
        string? callerContext,
        CancellationToken cancellationToken
    )
    {
        string? repoContext = null;
        if (
            templateName == "repo-assistant"
            && !string.IsNullOrWhiteSpace(_repoRootPath)
            && Directory.Exists(_repoRootPath)
        )
        {
            repoContext = await GenerateRepoContextAsync(_repoRootPath, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(repoContext))
            return callerContext;

        if (string.IsNullOrWhiteSpace(callerContext))
            return repoContext;

        return repoContext + "\n\n---\n\n" + callerContext;
    }

    private async Task<string> GenerateRepoContextAsync(
        string repoRoot,
        CancellationToken cancellationToken
    )
    {
        static bool IsProductionCode(string path) =>
            !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
            && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");

        var srcDir = Path.Combine(repoRoot, "src");
        var testsDir = Path.Combine(repoRoot, "tests");

        // Full content: every .cs file under src/ (Core, Infrastructure, Api)
        var contentFiles = Directory.Exists(srcDir)
            ? Directory
                .EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
                .Where(IsProductionCode)
                .OrderBy(f => f)
                .ToList()
            : [];

        // Test files — listed by name only to prove coverage without consuming tokens.
        var testFiles = Directory.Exists(testsDir)
            ? Directory
                .EnumerateFiles(testsDir, "*.cs", SearchOption.AllDirectories)
                .Where(IsProductionCode)
                .OrderBy(f => f)
                .ToList()
            : [];

        // Architecture doc drives the manifest — cache invalidates when the doc changes.
        var archDocPath = Path.Combine(repoRoot, "docs", "repo-assistant.md");
        var archDocFiles = File.Exists(archDocPath) ? [archDocPath] : Array.Empty<string>();

        // Cache invalidation: max LastWriteTimeUtc across all watched files.
        var allWatched = contentFiles.Concat(testFiles).Concat(archDocFiles).ToList();
        var stamp =
            allWatched.Count > 0
                ? allWatched.Max(f => File.GetLastWriteTimeUtc(f))
                : DateTime.MinValue;
        lock (this)
        {
            if (_contextCache.Stamp == stamp && !string.IsNullOrEmpty(_contextCache.Context))
                return _contextCache.Context;
        }

        var sb = new StringBuilder();

        // ── MANIFEST (always first — survives context-window truncation) ──────────
        // Sourced from docs/repo-assistant.md so the model always sees an accurate,
        // human-maintained description of what is already implemented.
        if (archDocFiles.Length > 0)
        {
            var archDoc = await File.ReadAllTextAsync(archDocPath, cancellationToken);
            sb.AppendLine(
                "## IMPORTANT: What is already implemented (from docs/repo-assistant.md)"
            );
            sb.AppendLine();
            sb.AppendLine(archDoc.TrimEnd());
            sb.AppendLine();
        }

        sb.AppendLine($"### Tests ({testFiles.Count} test files)");
        foreach (var f in testFiles)
            sb.AppendLine($"- {Path.GetRelativePath(repoRoot, f)}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // ── FILE TREE ─────────────────────────────────────────────────────────────
        sb.AppendLine("## Source file tree");
        sb.AppendLine("```");
        if (Directory.Exists(srcDir))
            AppendTree(sb, srcDir, repoRoot, 0);
        sb.AppendLine("```");

        // ── ROSLYN AST ────────────────────────────────────────────────────────────
        // Structural summaries: type declarations, base types, property and method
        // signatures for every production file. Compact and token-efficient.
        sb.AppendLine("\n## Structural API surface (Roslyn AST)");
        foreach (var file in contentFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(repoRoot, file);
            var source = await File.ReadAllTextAsync(file, cancellationToken);
            var summary = RoslynAstExtractor.ExtractSummary(source);
            if (string.IsNullOrWhiteSpace(summary))
                continue;
            sb.AppendLine($"\n### {relativePath}");
            sb.AppendLine("```");
            sb.AppendLine(summary);
            sb.AppendLine("```");
        }

        // ── CORE CONTRACTS (verbatim) ─────────────────────────────────────────────
        // Full source for Core only — the contracts the model must write against precisely.
        var coreDir = Path.Combine(repoRoot, "src", "Core");
        var coreFiles = contentFiles
            .Where(f => f.StartsWith(coreDir, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (coreFiles.Count > 0)
        {
            sb.AppendLine("\n## Full source: Core contracts");
            foreach (var file in coreFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(repoRoot, file);
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                sb.AppendLine($"\n### {relativePath}");
                sb.AppendLine("```csharp");
                sb.AppendLine(content.TrimEnd());
                sb.AppendLine("```");
            }
        }

        var result = sb.ToString();
        lock (this)
        {
            _contextCache = (stamp, result);
        }
        return result;
    }

    private static void AppendTree(StringBuilder sb, string dir, string repoRoot, int depth)
    {
        var indent = new string(' ', depth * 2);
        var dirName = Path.GetFileName(dir);
        if (dirName is "bin" or "obj")
            return;

        if (depth > 0)
            sb.AppendLine($"{indent}{dirName}/");

        foreach (var subDir in Directory.EnumerateDirectories(dir).OrderBy(d => d))
            AppendTree(sb, subDir, repoRoot, depth + 1);

        foreach (var file in Directory.EnumerateFiles(dir).OrderBy(f => f))
            sb.AppendLine($"{indent}  {Path.GetFileName(file)}");
    }

    private static string BuildUserMessage(string userGoal, string? projectArea)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"User goal: {userGoal}");

        if (!string.IsNullOrWhiteSpace(projectArea))
        {
            builder.AppendLine($"Project area: {projectArea}");
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
            var match = Regex.Match(
                yaml,
                $"^\\s*{Regex.Escape(key)}:[ \\t]*(.+)$",
                RegexOptions.Multiline
            );
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
        DateTime LastWriteTimeUtc,
        string Version
    );
}
