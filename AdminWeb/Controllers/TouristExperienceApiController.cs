using System.Net;
using System.Security.Claims;
using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services.Payments;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers.Api;

[ApiController]
[Route("api/tourist")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class TouristExperienceApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly VnPayPaymentService _vnPay;
    private readonly MomoPaymentService _momo;
    private readonly PaymentActivationService _activation;
    private readonly TouristAudioQuotaService _audioQuota;

    public TouristExperienceApiController(
        AppDbContext context,
        VnPayPaymentService vnPay,
        MomoPaymentService momo,
        PaymentActivationService activation,
        TouristAudioQuotaService audioQuota)
    {
        _context = context;
        _vnPay = vnPay;
        _momo = momo;
        _activation = activation;
        _audioQuota = audioQuota;
    }

    [HttpGet("premium")]
    public async Task<ActionResult<ApiResponse<TouristPremiumDto>>> Premium(CancellationToken cancellationToken)
    {
        var subscription = await ActiveSubscriptionAsync(GetTouristId(), cancellationToken);
        return Ok(ApiResponse<TouristPremiumDto>.Ok(new TouristPremiumDto
        {
            IsPremium = subscription != null,
            PlanName = subscription?.PaymentPlan?.PlanName ?? "Gói miễn phí",
            ExpiresAt = subscription?.ExpiresAt,
            VnPayAvailable = _vnPay.IsConfigured,
            MomoAvailable = _momo.CanCreatePayment,
            MomoDemoMode = _momo.IsDemoMode
        }));
    }

    [HttpPost("audio-play")]
    public async Task<ActionResult<ApiResponse<AudioPlayAccessDto>>> StartAudioPlayback(
        [FromBody] AudioPlayAccessRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _audioQuota.TryConsumeAsync(
            GetTouristId(),
            request.PoiId,
            request.LanguageCode,
            request.DeviceId,
            request.IsTts ? "MobileTtsPlay" : "MobileAudioPlay",
            cancellationToken: cancellationToken);

        if (!result.Allowed)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                ApiResponse<AudioPlayAccessDto>.Fail(result.Message));
        }

        return Ok(ApiResponse<AudioPlayAccessDto>.Ok(new AudioPlayAccessDto
        {
            IsPremium = result.IsPremium,
            DailyLimit = result.DailyLimit,
            UsedToday = result.UsedToday,
            RemainingToday = result.RemainingToday
        }, result.Message));
    }

    [AllowAnonymous]
    [HttpGet("plans")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TouristPaymentPlanDto>>>> Plans(
        CancellationToken cancellationToken)
    {
        var plans = (await _context.PaymentPlans
                .AsNoTracking()
                .Where(item => item.IsActive && (item.Audience == "Tourist" || item.Audience == "Both"))
                .ToListAsync(cancellationToken))
            .OrderBy(item => item.Price)
            .Select(item => new TouristPaymentPlanDto
            {
                Id = item.Id,
                PlanCode = item.PlanCode,
                PlanName = item.PlanName,
                Price = item.Price,
                DurationDays = item.DurationDays,
                Description = item.Description ?? ""
            })
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<TouristPaymentPlanDto>>.Ok(plans));
    }

    [HttpGet("payments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TouristPaymentDto>>>> Payments(
        CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var payments = await _context.PaymentTransactions
            .AsNoTracking()
            .Include(item => item.PaymentPlan)
            .Where(item => item.TouristId == touristId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(30)
            .Select(item => new TouristPaymentDto
            {
                Id = item.Id,
                TransactionCode = item.TransactionCode,
                PlanName = item.PaymentPlan != null ? item.PaymentPlan.PlanName : "Gói dịch vụ",
                Amount = item.Amount,
                Currency = item.Currency,
                PaymentMethod = item.PaymentMethod,
                Status = item.Status,
                CreatedAt = item.CreatedAt,
                PaidAt = item.PaidAt
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<TouristPaymentDto>>.Ok(payments));
    }

    [HttpPost("payments/checkout")]
    public async Task<ActionResult<ApiResponse<TouristCheckoutDto>>> Checkout(
        [FromBody] TouristCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var method = (request.PaymentMethod ?? "Manual").Trim();
        if (method is not ("Manual" or "VNPay" or "MoMo"))
            return BadRequest(ApiResponse<TouristCheckoutDto>.Fail("Phương thức thanh toán không hợp lệ."));

        var plan = await _context.PaymentPlans.FirstOrDefaultAsync(item =>
            item.Id == request.PlanId && item.IsActive &&
            (item.Audience == "Tourist" || item.Audience == "Both"), cancellationToken);
        if (plan == null)
            return NotFound(ApiResponse<TouristCheckoutDto>.Fail("Gói dịch vụ không tồn tại."));

        if (method == "VNPay" && !_vnPay.IsConfigured)
            return BadRequest(ApiResponse<TouristCheckoutDto>.Fail("VNPay chưa được cấu hình trên máy chủ."));
        if (method == "MoMo" && !_momo.CanCreatePayment)
            return BadRequest(ApiResponse<TouristCheckoutDto>.Fail("MoMo chưa được cấu hình trên máy chủ."));

        var touristId = GetTouristId();
        var payment = new PaymentTransaction
        {
            TransactionCode = NewTransactionCode(touristId),
            PayerType = "Tourist",
            TouristId = touristId,
            PaymentPlanId = plan.Id,
            Purpose = "TouristPremium",
            Amount = plan.Price,
            Currency = "VND",
            PaymentMethod = method,
            Status = "Pending",
            GatewayStatus = method == "Manual" ? "MANUAL_PENDING" : "CREATING",
            Note = $"Mua {plan.PlanName} từ ứng dụng VERSA Guide.",
            CreatedAt = DateTime.UtcNow
        };
        _context.PaymentTransactions.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        if (method == "Manual")
        {
            return Ok(ApiResponse<TouristCheckoutDto>.Ok(new TouristCheckoutDto
            {
                PaymentId = payment.Id,
                Status = payment.Status
            }, "Đã gửi yêu cầu. Gói sẽ được mở sau khi quản trị viên xác nhận."));
        }

        try
        {
            string checkoutUrl;
            if (method == "VNPay")
            {
                var returnUrl = AbsoluteUrl("/api/tourist/payments/vnpay-return");
                checkoutUrl = _vnPay.CreatePaymentUrl(payment, returnUrl, GetClientIpAddress());
                payment.GatewayOrderCode = payment.TransactionCode;
                payment.GatewayStatus = "PENDING";
            }
            else if (_momo.IsDemoMode)
            {
                var token = Guid.NewGuid().ToString("N");
                payment.GatewayPaymentLinkId = token;
                payment.GatewayOrderCode = payment.TransactionCode;
                payment.GatewayStatus = "MOMO_DEMO_PENDING";
                checkoutUrl = AbsoluteUrl($"/api/tourist/payments/demo/{payment.Id}?token={Uri.EscapeDataString(token)}");
            }
            else
            {
                var returnUrl = AbsoluteUrl("/api/tourist/payments/momo-return");
                var ipnUrl = AbsoluteUrl("/api/payment/momo/ipn");
                var result = await _momo.CreatePaymentAsync(
                    payment, $"VERSA Tourist {payment.Id}", returnUrl, ipnUrl, cancellationToken);
                checkoutUrl = result.PayUrl;
                payment.GatewayOrderCode = payment.TransactionCode;
                payment.GatewayPaymentLinkId = result.RequestId;
                payment.GatewayStatus = "PENDING";
            }

            payment.CheckoutUrl = checkoutUrl;
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(ApiResponse<TouristCheckoutDto>.Ok(new TouristCheckoutDto
            {
                PaymentId = payment.Id,
                Status = payment.Status,
                CheckoutUrl = checkoutUrl
            }, "Mở cổng thanh toán an toàn."));
        }
        catch (Exception exception)
        {
            payment.Status = "Cancelled";
            payment.GatewayStatus = "CREATE_FAILED";
            payment.Note = exception.Message;
            await _context.SaveChangesAsync(cancellationToken);
            return BadRequest(ApiResponse<TouristCheckoutDto>.Fail(exception.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("payments/demo/{id:int}")]
    public async Task<IActionResult> Demo(int id, [FromQuery] string token, CancellationToken cancellationToken)
    {
        var payment = await FindDemoPaymentAsync(id, token, cancellationToken);
        if (payment == null)
            return PaymentPage("Không tìm thấy giao dịch", "Liên kết thanh toán không hợp lệ hoặc đã hết hiệu lực.", false);

        var planName = WebUtility.HtmlEncode(payment.PaymentPlan?.PlanName ?? "Gói Premium");
        var action = $"/api/tourist/payments/demo/{payment.Id}/confirm?token={Uri.EscapeDataString(token)}";
        var body = $"<p>Bạn đang kích hoạt <strong>{planName}</strong>.</p><p class='price'>{payment.Amount:N0} {WebUtility.HtmlEncode(payment.Currency)}</p><form method='post' action='{action}'><button type='submit'>Xác nhận thanh toán demo</button></form>";
        return PaymentPage("Thanh toán MoMo demo", body, true, bodyIsHtml: true);
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost("payments/demo/{id:int}/confirm")]
    public async Task<IActionResult> ConfirmDemo(int id, [FromQuery] string token, CancellationToken cancellationToken)
    {
        var payment = await FindDemoPaymentAsync(id, token, cancellationToken);
        if (payment == null)
            return PaymentPage("Không thể xác nhận", "Giao dịch không hợp lệ.", false);

        if (payment.Status != "Paid")
            await _activation.MarkPaymentPaidAsync(payment, "MOMO_DEMO_PAID", cancellationToken);

        return PaymentPage("Thanh toán thành công", "Gói Premium đã được kích hoạt. Bạn có thể đóng trang này và quay lại ứng dụng.", true);
    }

    [AllowAnonymous]
    [HttpGet("payments/vnpay-return")]
    public async Task<IActionResult> VnPayReturn(CancellationToken cancellationToken)
    {
        var transactionCode = _vnPay.GetTxnRef(Request.Query);
        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.TransactionCode == transactionCode && item.TouristId != null, cancellationToken);

        if (payment == null || !_vnPay.VerifySignature(Request.Query))
            return PaymentPage("Thanh toán chưa hoàn tất", "Không xác minh được giao dịch VNPay.", false);

        payment.GatewayStatus = _vnPay.GetGatewayStatus(Request.Query);
        payment.GatewayPaymentLinkId = _vnPay.GetTransactionNo(Request.Query);
        if (_vnPay.IsSuccess(Request.Query))
        {
            await _activation.MarkPaymentPaidAsync(payment, "PAID", cancellationToken);
            return PaymentPage("Thanh toán thành công", "Gói Premium đã được kích hoạt. Hãy quay lại ứng dụng.", true);
        }

        payment.Status = payment.GatewayStatus == "24" ? "Cancelled" : payment.Status;
        await _context.SaveChangesAsync(cancellationToken);
        return PaymentPage("Thanh toán chưa hoàn tất", "VNPay chưa xác nhận giao dịch thành công.", false);
    }

    [AllowAnonymous]
    [HttpGet("payments/momo-return")]
    public async Task<IActionResult> MomoReturn(CancellationToken cancellationToken)
    {
        if (!_momo.VerifySignature(Request.Query))
            return PaymentPage("Thanh toán chưa hoàn tất", "Không xác minh được giao dịch MoMo.", false);

        var orderId = _momo.GetOrderId(Request.Query);
        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.TransactionCode == orderId && item.TouristId != null, cancellationToken);
        if (payment == null)
            return PaymentPage("Không tìm thấy giao dịch", "Vui lòng quay lại ứng dụng và kiểm tra lịch sử.", false);

        payment.GatewayStatus = _momo.GetGatewayStatus(Request.Query);
        payment.GatewayPaymentLinkId = _momo.GetTransactionId(Request.Query);
        if (_momo.IsSuccess(Request.Query))
        {
            await _activation.MarkPaymentPaidAsync(payment, "PAID", cancellationToken);
            return PaymentPage("Thanh toán thành công", "Gói Premium đã được kích hoạt. Hãy quay lại ứng dụng.", true);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return PaymentPage("Thanh toán chưa hoàn tất", "MoMo chưa xác nhận giao dịch thành công.", false);
    }

    private async Task<TouristSubscription?> ActiveSubscriptionAsync(int touristId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _context.TouristSubscriptions
            .AsNoTracking()
            .Include(item => item.PaymentPlan)
            .Where(item => item.TouristId == touristId && item.Status == "Active" && item.ExpiresAt > now)
            .OrderByDescending(item => item.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<PaymentTransaction?> FindDemoPaymentAsync(int id, string token, CancellationToken cancellationToken)
    {
        if (!_momo.IsDemoMode || string.IsNullOrWhiteSpace(token))
            return null;
        return await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item =>
                item.Id == id && item.TouristId != null && item.PaymentMethod == "MoMo" &&
                item.GatewayPaymentLinkId == token && item.Status != "Cancelled", cancellationToken);
    }

    private ContentResult PaymentPage(string title, string message, bool success, bool bodyIsHtml = false)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        var body = bodyIsHtml ? message : $"<p>{WebUtility.HtmlEncode(message)}</p>";
        var color = success ? "#16a36a" : "#d94a64";
        var html = $$"""
            <!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>{{safeTitle}}</title><style>body{margin:0;background:#f4f6fb;color:#172033;font-family:system-ui;display:grid;min-height:100vh;place-items:center;padding:20px}main{width:min(460px,100%);background:white;border:1px solid #e4e8f0;border-radius:28px;padding:30px;box-shadow:0 20px 60px #1720331c;text-align:center}.icon{width:64px;height:64px;border-radius:22px;background:{{color}};color:white;display:grid;place-items:center;margin:auto;font-size:30px}h1{font-size:25px}p{color:#68748a;line-height:1.6}.price{font-size:28px;color:#172033;font-weight:800}button{border:0;border-radius:16px;padding:14px 20px;background:#5b3fe4;color:white;font-weight:700;font-size:16px;width:100%}</style></head>
            <body><main><div class="icon">{{(success ? "✓" : "!")}}</div><h1>{{safeTitle}}</h1>{{body}}</main></body></html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }

    private int GetTouristId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string AbsoluteUrl(string path) => $"{Request.Scheme}://{Request.Host}{Request.PathBase}{path}";
    private string GetClientIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    private static string NewTransactionCode(int touristId) =>
        $"USR-{DateTime.UtcNow:yyyyMMddHHmmss}-{touristId}-{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant();
}
