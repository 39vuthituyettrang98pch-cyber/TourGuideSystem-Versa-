using AdminWeb.Data;
using AdminWeb.Services.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

/// <summary>
/// Safety router for stale/wrong payment URLs such as /Editor/Payments/VnPayReturn
/// or /Editor/Payments/ConfirmMomoDemo. This prevents payment callbacks from
/// falling into Editor/Admin 404 pages when a browser has multiple role cookies.
/// </summary>
public sealed class PaymentReturnRouterController : Controller
{
    private readonly AppDbContext _context;
    private readonly PaymentActivationService _activation;

    public PaymentReturnRouterController(AppDbContext context, PaymentActivationService activation)
    {
        _context = context;
        _activation = activation;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? id, CancellationToken cancellationToken)
    {
        if (id.HasValue)
            return await RedirectByPaymentIdAsync(id.Value, cancellationToken);

        return LocalRedirect("/Owner/Payments");
    }

    [HttpGet]
    public async Task<IActionResult> VnPayReturn(CancellationToken cancellationToken)
    {
        var txnRef = Request.Query["vnp_TxnRef"].ToString();
        var queryString = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;

        if (!string.IsNullOrWhiteSpace(txnRef))
        {
            var payment = await _context.PaymentTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TransactionCode == txnRef, cancellationToken);

            if (payment?.OwnerProfileId != null || txnRef.StartsWith("OWN", StringComparison.OrdinalIgnoreCase))
                return LocalRedirect($"/Owner/Payments/VnPayReturn{queryString}");

            if (payment?.TouristId != null || txnRef.StartsWith("USR", StringComparison.OrdinalIgnoreCase))
                return LocalRedirect($"/DuKhach/Payments/VnPayReturn{queryString}");
        }

        return LocalRedirect("/DuKhach/Payments/My?vnpayCancelled=1");
    }


    [HttpGet]
    public IActionResult Plans() => LocalRedirect("/DuKhach/Payments/Plans");

    [HttpGet]
    public IActionResult My() => LocalRedirect("/DuKhach/Payments/My");

    // These POST bridges protect users from stale/wrong generated forms such as
    // /Editor/Payments/PurchaseVnPay or /Admin/Payments/CreateMomo.
    // RedirectPreserveMethod keeps the original POST body and antiforgery token,
    // then sends the request to the real portal endpoint.
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult Purchase() => RedirectPreserveMethod("/DuKhach/Payments/Purchase");

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult PurchaseVnPay() => RedirectPreserveMethod("/DuKhach/Payments/PurchaseVnPay");

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult PurchaseMomo() => RedirectPreserveMethod("/DuKhach/Payments/PurchaseMomo");

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult ActivateFree() => RedirectPreserveMethod("/Owner/Payments/ActivateFree");

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult Create() => RedirectPreserveMethod("/Owner/Payments/Create");

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult CreateVnPay() => RedirectPreserveMethod("/Owner/Payments/CreateVnPay");

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult CreateMomo() => RedirectPreserveMethod("/Owner/Payments/CreateMomo");

    [HttpGet]
    public async Task<IActionResult> MomoReturn(CancellationToken cancellationToken)
    {
        var orderId = Request.Query["orderId"].ToString();
        var queryString = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;

        if (!string.IsNullOrWhiteSpace(orderId))
        {
            var payment = await _context.PaymentTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TransactionCode == orderId, cancellationToken);

            if (payment?.OwnerProfileId != null || orderId.StartsWith("OWN", StringComparison.OrdinalIgnoreCase))
                return LocalRedirect($"/Owner/Payments/MomoReturn{queryString}");

            if (payment?.TouristId != null || orderId.StartsWith("USR", StringComparison.OrdinalIgnoreCase))
                return LocalRedirect($"/DuKhach/Payments/MomoReturn{queryString}");
        }

        return LocalRedirect("/DuKhach/Payments/My?momoReturn=1");
    }

    [HttpGet]
    public async Task<IActionResult> MomoDemo(int id, CancellationToken cancellationToken)
    {
        var payment = await _context.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (payment?.OwnerProfileId != null)
            return LocalRedirect($"/Owner/Payments/MomoDemo/{id}");

        if (payment?.TouristId != null)
            return LocalRedirect($"/DuKhach/Payments/MomoDemo/{id}");

        return LocalRedirect("/DuKhach/Payments/Plans");
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ConfirmMomoDemo(int id, CancellationToken cancellationToken)
    {
        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (payment == null)
            return LocalRedirect("/DuKhach/Payments/Plans?paymentNotFound=1");

        if (payment.Status != "Paid")
        {
            payment.GatewayStatus = "MOMO_DEMO_PAID";
            payment.GatewayPaymentLinkId ??= $"MOMO-DEMO-{payment.Id}";
            await _activation.MarkPaymentPaidAsync(payment, "MOMO_DEMO_PAID", cancellationToken);
        }

        if (payment.OwnerProfileId != null)
            return LocalRedirect("/Owner/Payments?momoDemoPaid=1");

        if (payment.TouristId != null)
            return LocalRedirect("/DuKhach/Payments/My?momoDemoPaid=1");

        return LocalRedirect("/DuKhach/Payments/Plans");
    }

    private async Task<IActionResult> RedirectByPaymentIdAsync(int id, CancellationToken cancellationToken)
    {
        var payment = await _context.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (payment?.OwnerProfileId != null)
            return LocalRedirect("/Owner/Payments");

        if (payment?.TouristId != null)
            return LocalRedirect("/DuKhach/Payments/My");

        return LocalRedirect("/DuKhach/Payments/Plans");
    }
}
