using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AdminWeb.Services;

public interface IGeminiService
{
    Task<string> OptimizePoiContentAsync(string rawText, CancellationToken cancellationToken = default);
}

public sealed class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    private static string? _cachedModel;
    private static readonly SemaphoreSlim ModelLock = new(1, 1);

    public GeminiService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> OptimizePoiContentAsync(
        string rawText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            throw new ArgumentException("Nội dung thô đang trống.", nameof(rawText));

        var apiKey = _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Chưa cấu hình Gemini:ApiKey. Local dùng User Secrets; host dùng biến môi trường Gemini__ApiKey.");

        var model = await PickModelForGenerateContentAsync(apiKey, cancellationToken);

        var prompt =
            "Bạn là AI Content Writer cho ứng dụng du lịch.\n\n" +
            "Nhiệm vụ:\n" +
            "- Viết lại nội dung mô tả POI sao cho hấp dẫn, tự nhiên, đúng ngữ cảnh du lịch.\n" +
            "- Ngôn ngữ: tiếng Việt.\n" +
            "- Tránh bịa đặt thông tin mới, không tự thêm địa danh/sự kiện chưa có trong text.\n" +
            "- Không dùng giọng văn quá quảng cáo.\n\n" +
            "Yêu cầu output:\n" +
            "Trả về ĐÚNG một JSON object với 2 field: \"short\" và \"full\".\n" +
            "short: 2-3 câu, dễ đọc, thu hút.\n" +
            "full: 4-8 câu, phong phú hơn, có thể có gợi ý trải nghiệm.\n\n" +
            "Ví dụ output hợp lệ:\n" +
            "{ \"short\": \"...\", \"full\": \"...\" }\n\n" +
            "Nội dung thô cần tối ưu:\n" +
            rawText;

        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.7,
                topP = 0.9,

                // Không để 80000 vì rất dễ chạm quota/rate limit.
                // Mô tả POI chỉ cần khoảng 800-1200 token là đủ.
                maxOutputTokens = 1200
            }
        };

        const int maxRetries = 4;
        var delayMs = 1500;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var geminiText = ExtractGeminiText(body);
                var result = ParseOptimizedContent(geminiText);

                return $"{result.Short}|||{result.Full}";
            }

            var statusCode = (int)response.StatusCode;

            if ((statusCode == 429 || statusCode == 503) && attempt < maxRetries - 1)
            {
                var delay = statusCode == 429
                    ? GetRetryDelay(response, body, delayMs)
                    : TimeSpan.FromMilliseconds(delayMs);

                await Task.Delay(delay, cancellationToken);

                delayMs *= 2;
                continue;
            }

            if (statusCode == 429)
            {
                throw new InvalidOperationException(
                    "Gemini đang bị giới hạn lượt dùng miễn phí/rate limit 429. " +
                    "Vui lòng đợi khoảng 1 phút rồi thử lại, hoặc bật billing/nâng hạn mức trong Google AI Studio."
                );
            }

            if (statusCode == 503)
            {
                throw new InvalidOperationException(
                    "Gemini đang quá tải 503. Vui lòng thử lại sau vài chục giây."
                );
            }

            throw new InvalidOperationException(
                $"Gemini API error: {statusCode}. Chi tiết: {GetShortErrorMessage(body)}"
            );
        }

        throw new InvalidOperationException("Gemini request failed unexpectedly.");
    }

    private async Task<string> PickModelForGenerateContentAsync(
        string apiKey,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_cachedModel))
            return _cachedModel;

        await ModelLock.WaitAsync(cancellationToken);

        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedModel))
                return _cachedModel;

            var configuredModel = _configuration["Gemini:Model"];

            var listUrl =
                $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";

            using var resp = await _httpClient.GetAsync(listUrl, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Gemini ListModels error: {(int)resp.StatusCode}. Chi tiết: {GetShortErrorMessage(body)}"
                );

            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("models", out var modelsEl) ||
                modelsEl.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Gemini ListModels trả về dữ liệu không hợp lệ.");
            }

            var candidates = new List<string>();

            foreach (var m in modelsEl.EnumerateArray())
            {
                var name = m.TryGetProperty("name", out var n)
                    ? n.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var cleanName = name.StartsWith("models/")
                    ? name["models/".Length..]
                    : name;

                if (!cleanName.Contains("gemini", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (cleanName.Contains("embedding", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!SupportsGenerateContent(m))
                    continue;

                candidates.Add(cleanName);
            }

            if (!string.IsNullOrWhiteSpace(configuredModel))
            {
                var cleanConfiguredModel = configuredModel.StartsWith("models/")
                    ? configuredModel["models/".Length..]
                    : configuredModel;

                var matched = candidates.FirstOrDefault(x =>
                    string.Equals(x, cleanConfiguredModel, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(matched))
                {
                    _cachedModel = matched;
                    return _cachedModel;
                }
            }

            var preferred =
                candidates.FirstOrDefault(x => x.Contains("flash", StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(preferred))
                throw new InvalidOperationException("Không tìm thấy Gemini model nào hỗ trợ generateContent.");

            _cachedModel = preferred;
            return _cachedModel;
        }
        finally
        {
            ModelLock.Release();
        }
    }

    private static bool SupportsGenerateContent(JsonElement modelElement)
    {
        if (!modelElement.TryGetProperty("supportedGenerationMethods", out var methodsEl) ||
            methodsEl.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        foreach (var method in methodsEl.EnumerateArray())
        {
            var value = method.GetString() ?? "";

            if (value.Contains("generateContent", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ExtractGeminiText(string body)
    {
        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Gemini không trả về candidates.");
        }

        var first = candidates[0];

        var text = first
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Gemini returned empty text.");

        return text.Trim();
    }

    private static (string Short, string Full) ParseOptimizedContent(string text)
    {
        var cleaned = CleanGeminiJsonText(text);

        JsonDocument outDoc;

        try
        {
            outDoc = JsonDocument.Parse(cleaned);
        }
        catch
        {
            var jsonOnly = ExtractFirstJsonObject(cleaned);
            outDoc = JsonDocument.Parse(jsonOnly);
        }

        using (outDoc)
        {
            var shortText = outDoc.RootElement.TryGetProperty("short", out var st)
                ? st.GetString() ?? ""
                : "";

            var fullText = outDoc.RootElement.TryGetProperty("full", out var ft)
                ? ft.GetString() ?? ""
                : "";

            if (string.IsNullOrWhiteSpace(shortText) &&
                string.IsNullOrWhiteSpace(fullText))
            {
                throw new InvalidOperationException("Gemini JSON không có field short/full hợp lệ.");
            }

            return (shortText.Trim(), fullText.Trim());
        }
    }

    private static string CleanGeminiJsonText(string text)
    {
        var cleaned = text.Trim();

        cleaned = cleaned.Replace("```json", "", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("```", "", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Trim();

        return cleaned;
    }

    private static string ExtractFirstJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Cannot extract JSON object from empty text.");

        var start = text.IndexOf('{');

        if (start < 0)
            throw new InvalidOperationException("Cannot find start '{' for JSON object.");

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '{')
                depth++;
            else if (c == '}')
                depth--;

            if (depth == 0)
            {
                var len = i - start + 1;
                return text.Substring(start, len);
            }
        }

        throw new InvalidOperationException("Cannot extract complete JSON object.");
    }

    private static TimeSpan GetRetryDelay(
        HttpResponseMessage response,
        string body,
        int fallbackMs)
    {
        if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
            return delta;

        var retryInMatch = Regex.Match(
            body,
            @"retry in\s+([0-9]+(?:\.[0-9]+)?)s",
            RegexOptions.IgnoreCase
        );

        if (retryInMatch.Success &&
            double.TryParse(
                retryInMatch.Groups[1].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var retryInSeconds))
        {
            return TimeSpan.FromSeconds(Math.Ceiling(retryInSeconds) + 1);
        }

        var retryDelayMatch = Regex.Match(
            body,
            @"""retryDelay""\s*:\s*""([0-9]+(?:\.[0-9]+)?)s""",
            RegexOptions.IgnoreCase
        );

        if (retryDelayMatch.Success &&
            double.TryParse(
                retryDelayMatch.Groups[1].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var retryDelaySeconds))
        {
            return TimeSpan.FromSeconds(Math.Ceiling(retryDelaySeconds) + 1);
        }

        return TimeSpan.FromMilliseconds(fallbackMs);
    }

    private static string GetShortErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "Không có nội dung lỗi.";

        try
        {
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var code = error.TryGetProperty("code", out var codeEl)
                    ? codeEl.ToString()
                    : "";

                var message = error.TryGetProperty("message", out var msgEl)
                    ? msgEl.GetString()
                    : "";

                var status = error.TryGetProperty("status", out var statusEl)
                    ? statusEl.GetString()
                    : "";

                return $"Code={code}, Status={status}, Message={message}";
            }
        }
        catch
        {
            // Ignore parse error.
        }

        return body.Length > 500
            ? body[..500] + "..."
            : body;
    }
}
