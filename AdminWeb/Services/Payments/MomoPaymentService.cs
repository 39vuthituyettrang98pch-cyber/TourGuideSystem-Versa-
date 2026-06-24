using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdminWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace AdminWeb.Services.Payments;

public sealed class MomoPaymentService
{
    private const string DefaultEndpoint = "https://test-payment.momo.vn/v2/gateway/api/create";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public MomoPaymentService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public bool IsDemoMode =>
        bool.TryParse(_configuration["MoMo:DemoMode"], out var demoMode) && demoMode;

    public bool IsConfigured =>
        IsRealValue(PartnerCode) &&
        IsRealValue(AccessKey) &&
        IsRealValue(SecretKey) &&
        IsRealValue(Endpoint);

    public bool CanCreatePayment => IsConfigured || IsDemoMode;

    private string PartnerCode => _configuration["MoMo:PartnerCode"] ?? string.Empty;
    private string AccessKey => _configuration["MoMo:AccessKey"] ?? string.Empty;
    private string SecretKey => _configuration["MoMo:SecretKey"] ?? string.Empty;
    private string Endpoint => string.IsNullOrWhiteSpace(_configuration["MoMo:Endpoint"])
        ? DefaultEndpoint
        : _configuration["MoMo:Endpoint"]!.Trim();

    public async Task<MomoCreatePaymentResult> CreatePaymentAsync(
        PaymentTransaction payment,
        string orderInfo,
        string redirectUrl,
        string ipnUrl,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var amount = ((long)decimal.Round(payment.Amount, 0, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
        var orderId = payment.TransactionCode;
        var requestId = $"{orderId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var requestType = "captureWallet";
        var extraData = string.Empty;

        var rawSignature =
            $"accessKey={AccessKey}&amount={amount}&extraData={extraData}&ipnUrl={ipnUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={PartnerCode}&redirectUrl={redirectUrl}&requestId={requestId}&requestType={requestType}";

        var payload = new MomoCreatePaymentRequest
        {
            PartnerCode = PartnerCode,
            AccessKey = AccessKey,
            RequestId = requestId,
            Amount = amount,
            OrderId = orderId,
            OrderInfo = orderInfo,
            RedirectUrl = redirectUrl,
            IpnUrl = ipnUrl,
            ExtraData = extraData,
            RequestType = requestType,
            Lang = "vi",
            Signature = HmacSha256(SecretKey, rawSignature)
        };

        using var response = await _httpClient.PostAsJsonAsync(Endpoint, payload, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<MomoCreatePaymentResponse>(content, JsonOptions);

        if (!response.IsSuccessStatusCode || result == null || result.ResultCode != 0 || string.IsNullOrWhiteSpace(result.PayUrl))
        {
            var message = result?.Message ?? response.ReasonPhrase ?? "Không tạo được link thanh toán MoMo.";
            throw new InvalidOperationException($"MoMo error: {message}");
        }

        return new MomoCreatePaymentResult(result.PayUrl, result.Deeplink, result.QrCodeUrl, result.ResultCode, result.Message, requestId);
    }

    public bool VerifySignature(IQueryCollection query)
    {
        var values = query.ToDictionary(item => item.Key, item => item.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        return VerifySignature(values);
    }

    public bool VerifySignature(JsonElement json)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in json.EnumerateObject())
            values[property.Name] = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? string.Empty : property.Value.GetRawText();
        return VerifySignature(values);
    }

    public bool VerifySignature(IReadOnlyDictionary<string, string> values)
    {
        if (!values.TryGetValue("signature", out var signature) || string.IsNullOrWhiteSpace(signature))
            return false;

        var raw = BuildReturnSignatureRaw(values);
        var expected = HmacSha256(SecretKey, raw);
        return FixedEquals(expected, signature);
    }

    public bool IsSuccess(IQueryCollection query) => query.TryGetValue("resultCode", out var code) && code.ToString() == "0";

    public bool IsSuccess(JsonElement json) =>
        json.TryGetProperty("resultCode", out var resultCode) &&
        (resultCode.ValueKind == JsonValueKind.Number ? resultCode.GetInt32() == 0 : resultCode.GetString() == "0");

    public string GetOrderId(IQueryCollection query) => query["orderId"].ToString();

    public string GetOrderId(JsonElement json) =>
        json.TryGetProperty("orderId", out var orderId) ? orderId.GetString() ?? string.Empty : string.Empty;

    public string GetGatewayStatus(IQueryCollection query) => query["resultCode"].ToString();

    public string GetGatewayStatus(JsonElement json) =>
        json.TryGetProperty("resultCode", out var resultCode) ? resultCode.GetRawText().Trim('"') : string.Empty;

    public string? GetTransactionId(IQueryCollection query) => query["transId"].ToString();

    public string? GetTransactionId(JsonElement json) =>
        json.TryGetProperty("transId", out var transId) ? transId.GetRawText().Trim('"') : null;

    private string BuildReturnSignatureRaw(IReadOnlyDictionary<string, string> values)
    {
        string Get(string key) => values.TryGetValue(key, out var value) ? value : string.Empty;
        return
            $"accessKey={AccessKey}&amount={Get("amount")}&extraData={Get("extraData")}&message={Get("message")}&orderId={Get("orderId")}&orderInfo={Get("orderInfo")}&orderType={Get("orderType")}&partnerCode={Get("partnerCode")}&payType={Get("payType")}&requestId={Get("requestId")}&responseTime={Get("responseTime")}&resultCode={Get("resultCode")}&transId={Get("transId")}";
    }

    private static string HmacSha256(string key, string input)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static bool FixedEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left.ToLowerInvariant());
        var rightBytes = Encoding.UTF8.GetBytes(right.ToLowerInvariant());
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static bool IsRealValue(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Contains("CHANGE", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);

    private void EnsureConfigured()
    {
        if (IsDemoMode)
            throw new InvalidOperationException("MoMo đang bật DemoMode. Không gọi API MoMo thật.");

        if (!IsConfigured)
            throw new InvalidOperationException("Chưa cấu hình MoMo: MoMo:PartnerCode, MoMo:AccessKey, MoMo:SecretKey và MoMo:Endpoint.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public sealed record MomoCreatePaymentResult(string PayUrl, string? Deeplink, string? QrCodeUrl, int ResultCode, string? Message, string RequestId);

public sealed class MomoCreatePaymentRequest
{
    [JsonPropertyName("partnerCode")]
    public string PartnerCode { get; set; } = string.Empty;

    [JsonPropertyName("accessKey")]
    public string AccessKey { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public string Amount { get; set; } = string.Empty;

    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("orderInfo")]
    public string OrderInfo { get; set; } = string.Empty;

    [JsonPropertyName("redirectUrl")]
    public string RedirectUrl { get; set; } = string.Empty;

    [JsonPropertyName("ipnUrl")]
    public string IpnUrl { get; set; } = string.Empty;

    [JsonPropertyName("extraData")]
    public string ExtraData { get; set; } = string.Empty;

    [JsonPropertyName("requestType")]
    public string RequestType { get; set; } = string.Empty;

    [JsonPropertyName("lang")]
    public string Lang { get; set; } = "vi";

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}

public sealed class MomoCreatePaymentResponse
{
    [JsonPropertyName("partnerCode")]
    public string? PartnerCode { get; set; }

    [JsonPropertyName("orderId")]
    public string? OrderId { get; set; }

    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }

    [JsonPropertyName("amount")]
    public long? Amount { get; set; }

    [JsonPropertyName("responseTime")]
    public long? ResponseTime { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("resultCode")]
    public int ResultCode { get; set; }

    [JsonPropertyName("payUrl")]
    public string? PayUrl { get; set; }

    [JsonPropertyName("deeplink")]
    public string? Deeplink { get; set; }

    [JsonPropertyName("qrCodeUrl")]
    public string? QrCodeUrl { get; set; }
}
