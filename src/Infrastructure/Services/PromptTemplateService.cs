using System.Text;
using System.Text.RegularExpressions;
using Core.Models;
using Core.Services;

namespace Infrastructure.Services;

/// <summary>
/// Scans a directory of <c>.prompty</c> files and exposes their metadata (name, description,
/// declared inputs) to the UI so it can render a template dropdown with dynamic input fields.
/// </summary>
public sealed class PromptTemplateService(string promptsDirectory) : IPromptTemplateService
{
    public async Task<IReadOnlyList<PromptTemplateInfo>> ListAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!Directory.Exists(promptsDirectory))
            return [];

        var files = Directory
            .EnumerateFiles(promptsDirectory, "*.prompty", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f)
            .ToList();

        var results = new List<PromptTemplateInfo>(files.Count);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var raw = await File.ReadAllTextAsync(file, cancellationToken);
            var info = ParseTemplate(raw);
            results.Add(info);
        }

        return results;
    }

    // ── Parsing ───────────────────────────────────────────────────────────────

    private static PromptTemplateInfo ParseTemplate(string rawPrompt)
    {
        // Extract YAML front matter between the first pair of --- delimiters.
        var parts = Regex.Split(rawPrompt, @"^---\r?$", RegexOptions.Multiline);
        var yaml = parts.Length >= 3 ? parts[1] : string.Empty;

        var name = ExtractScalar(yaml, "name");
        var description = ExtractScalar(yaml, "description");
        var inputs = ParseInputs(yaml);

        return new PromptTemplateInfo(
            Name: string.IsNullOrWhiteSpace(name) ? "unknown" : name,
            Description: string.IsNullOrWhiteSpace(description) ? string.Empty : description,
            Inputs: inputs
        );
    }

    /// <summary>
    /// Parses the <c>inputs:</c> block from the YAML front matter.
    /// Each input key lives at 2-space indent; its properties at 4-space indent.
    /// </summary>
    private static IReadOnlyList<PromptInputInfo> ParseInputs(string yaml)
    {
        var inputs = new List<PromptInputInfo>();
        var lines = yaml.Split('\n');

        var inInputs = false;
        string? currentKey = null;
        string? currentType = null;
        string? currentDescription = null;
        string? currentDefault = null;

        void Flush()
        {
            if (currentKey is null)
                return;
            inputs.Add(
                new PromptInputInfo(
                    Key: currentKey,
                    Type: currentType ?? "string",
                    Description: currentDescription ?? string.Empty,
                    Default: currentDefault
                )
            );
            currentKey = currentType = currentDescription = currentDefault = null;
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            if (line == "inputs:")
            {
                inInputs = true;
                continue;
            }

            if (!inInputs)
                continue;

            // Any non-blank line at column 0 ends the inputs block.
            if (line.Length > 0 && line[0] != ' ')
            {
                Flush();
                break;
            }

            // 2-space-indented key: (e.g. "  user_goal:")
            if (Regex.IsMatch(line, @"^  \w+:$"))
            {
                Flush();
                currentKey = line.Trim().TrimEnd(':');
                continue;
            }

            // 4-space-indented property: value (e.g. "    type: string")
            if (currentKey is not null && line.StartsWith("    ", StringComparison.Ordinal))
            {
                var m = Regex.Match(line, @"^    (type|description|default):[ \t]*(.*)$");
                if (m.Success)
                {
                    var val = m.Groups[2].Value.Trim().Trim('"');
                    switch (m.Groups[1].Value)
                    {
                        case "type":
                            currentType = val;
                            break;
                        case "description":
                            currentDescription = val;
                            break;
                        case "default":
                            currentDefault = val;
                            break;
                    }
                }
            }
        }

        Flush();
        return inputs;
    }

    private static string ExtractScalar(string yaml, string key)
    {
        // Handles both single-line ("name: value") and block scalar ("description: >\n  ...").
        var match = Regex.Match(
            yaml,
            $@"^[ \t]*{Regex.Escape(key)}:[ \t]*(.*)$",
            RegexOptions.Multiline
        );
        if (!match.Success)
            return string.Empty;

        var inlineValue = match.Groups[1].Value.Trim().Trim('"');

        // If the value is a block scalar indicator (> or |), read indented continuation lines.
        if (inlineValue is ">" or "|" or ">-" or "|-")
        {
            var lines = yaml.Split('\n');
            var startIndex = yaml[..match.Index].Split('\n').Length; // line after the key
            var sb = new StringBuilder();
            for (var i = startIndex; i < lines.Length; i++)
            {
                var line = lines[i];
                // Continuation lines must be indented; a non-indented line ends the block.
                if (line.Length > 0 && line[0] != ' ' && line[0] != '\t')
                    break;
                sb.AppendLine(line.TrimStart());
            }
            return sb.ToString().Trim();
        }

        return inlineValue;
    }
}
