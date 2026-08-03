using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace LoadIx.Infrastructure.Identity;

/// <summary>
/// Produces bounded, non-secret audit projections. Audit entries must never be
/// used as a second copy of a request or a document.
/// </summary>
public static partial class AuditSummary
{
    private const int MaxJsonLength = 3_500;
    private const int MaxTextLength = 500;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        MaxDepth = 8,
    };

    public static string? Serialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            var node = JsonSerializer.SerializeToNode(value, SerializerOptions);
            var sanitized = SanitizeNode(node);
            return TruncateJson(sanitized is null
                ? null
                : sanitized.ToJsonString(SerializerOptions));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    public static string? SanitizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(json);
            var sanitized = SanitizeNode(node);
            return TruncateJson(sanitized is null
                ? null
                : sanitized.ToJsonString(SerializerOptions));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? SanitizeText(string? value, int maxLength = MaxTextLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = EmbeddedSecretPattern().Replace(value, "[REDACTED]");
        return sanitized.Length <= maxLength
            ? sanitized
            : sanitized[..maxLength];
    }

    private static JsonNode? SanitizeNode(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonObject jsonObject)
        {
            var safeObject = new JsonObject();
            foreach (var property in jsonObject)
            {
                if (IsSensitiveProperty(property.Key))
                {
                    continue;
                }

                safeObject[property.Key] = SanitizeNode(property.Value);
            }

            return safeObject;
        }

        if (node is JsonArray jsonArray)
        {
            var safeArray = new JsonArray();
            foreach (var item in jsonArray)
            {
                safeArray.Add(SanitizeNode(item));
            }

            return safeArray;
        }

        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
        {
            return JsonValue.Create(SanitizeText(text, MaxJsonLength));
        }

        return node.DeepClone();
    }

    private static bool IsSensitiveProperty(string propertyName)
    {
        var normalized = propertyName
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalized.Contains("password", StringComparison.Ordinal)
            || normalized.Contains("token", StringComparison.Ordinal)
            || normalized.Contains("secret", StringComparison.Ordinal)
            || normalized.Contains("authorization", StringComparison.Ordinal)
            || normalized.Contains("cookie", StringComparison.Ordinal)
            || normalized.Contains("apikey", StringComparison.Ordinal)
            || normalized is "body"
                or "file"
                or "filebody"
                or "filecontents"
                or "content"
                or "contentbytes"
                or "rawcontent"
                or "contentbody"
                or "bytes"
                or "stream"
                or "payload"
                or "storagekey"
                or "privatekey";
    }

    private static string? TruncateJson(string? json) =>
        string.IsNullOrEmpty(json)
            ? null
            : json.Length <= MaxJsonLength
                ? json
                : "{\"truncated\":true}";

    [GeneratedRegex(
        "(?ix)(?:\\b(?:bearer|basic)\\s+[^\\s,;]+)|(?:\\b(?:password|token|secret|api[-_]?key|authorization|cookie)\\s*[:=]\\s*[^\\s,;]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex EmbeddedSecretPattern();
}
