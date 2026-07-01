using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using AdminWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace AdminWeb.Services.Payments;

public sealed class VnPayPaymentService
{
    private const string DefaultPaymentUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    private readonly IConfiguration _configuration;

    public VnPayPaymentService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsConfigured =>
        IsRealValue(TmnCode) &&
        IsRealValue(HashSecret) &&
        IsRealValue(PaymentUrl);

    private string TmnCode => _configuration["VnPay:TmnCode"] ?? string.Empty;
    private string HashSecret => _configuration["VnPay:HashSecret"] ?? string.Empty;
    private string PaymentUrl => string.IsNullOrWhiteSpace(_configuration["VnPay:PaymentUrl"])
        ? DefaultPaymentUrl
        : _configuration["VnPay:PaymentUrl"]!.Trim();

    public string CreatePaymentUrl(PaymentTransaction payment, string returnUrl, string ipAddress)
    {
        EnsureConfigured();

        var amount = (long)decimal.Round(payment.Amount, 0, MidpointRounding.AwayFromZero) * 100L;
        var createDate = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var txnRef = NormalizeTxnRef(payment.TransactionCode);

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = TmnCode,
            ["vnp_Amount"] = amount.ToString(CultureInfo.InvariantCulture),
            ["vnp_CreateDate"] = createDate,
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = NormalizeOrderInfo(payment.Note ?? $"VERSA {txnRef}"),
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_TxnRef"] = txnRef
        };

        var query = BuildQuery(parameters);
        var secureHash = HmacSha512(HashSecret, query);
        return $"{PaymentUrl}?{query}&vnp_SecureHash={secureHash}";
    }

    public bool VerifySignature(IQueryCollection query)
    {
        var providedHash = query["vnp_SecureHash"].ToString();
        if (string.IsNullOrWhiteSpace(providedHash) || string.IsNullOrWhiteSpace(HashSecret))
            return false;

        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in query)
        {
            if (item.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.Key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.Key, "vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            {
                values[item.Key] = item.Value.ToString();
            }
        }

        var raw = BuildQuery(values);
        var expected = HmacSha512(HashSecret, raw);
        return FixedEquals(expected, providedHash);
    }

    public bool IsSuccess(IQueryCollection query) =>
        string.Equals(query["vnp_ResponseCode"].ToString(), "00", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(query["vnp_TransactionStatus"].ToString(), "00", StringComparison.OrdinalIgnoreCase);

    public string GetTxnRef(IQueryCollection query) => query["vnp_TxnRef"].ToString();
    public string GetGatewayStatus(IQueryCollection query) => query["vnp_ResponseCode"].ToString();
    public string? GetTransactionNo(IQueryCollection query) => query["vnp_TransactionNo"].ToString();

    private static string BuildQuery(SortedDictionary<string, string> values) =>
        string.Join('&', values
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{WebUtility.UrlEncode(item.Key)}={WebUtility.UrlEncode(item.Value)}"));

    private static string HmacSha512(string key, string input)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool FixedEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left.ToLowerInvariant());
        var rightBytes = Encoding.UTF8.GetBytes(right.ToLowerInvariant());
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string NormalizeTxnRef(string value)
    {
        var cleaned = new string((value ?? string.Empty)
            .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
            .ToArray());

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

        return cleaned.Length <= 100 ? cleaned : cleaned[..100];
    }

    private static string NormalizeOrderInfo(string value)
    {
        var cleaned = string.Join(' ', (value ?? "VERSA Payment").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return cleaned.Length <= 180 ? cleaned : cleaned[..180];
    }

    private static bool IsRealValue(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Contains("CHANGE", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Chưa cấu hình VNPay: VnPay:TmnCode, VnPay:HashSecret và VnPay:PaymentUrl.");
    }
}
