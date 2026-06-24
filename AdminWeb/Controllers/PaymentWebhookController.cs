using System.Text.Json;
using AdminWeb.Data;
using AdminWeb.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payment")]
public sealed class PaymentWebhookController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly VnPayPaymentService _vnPay;
    private readonly MomoPaymentService _momo;
    private readonly PaymentActivationService _activation;

    public PaymentWebhookController(
        AppDbContext context,
        VnPayPaymentService vnPay,
        MomoPaymentService momo,
        PaymentActivationService activation)
    {
        _context = context;
        _vnPay = vnPay;
        _momo = momo;
        _activation = activation;
    }

    [HttpGet("vnpay/ipn")]
    public async Task<IActionResult> VnPayIpn(CancellationToken cancellationToken)
    {
        if (!_vnPay.VerifySignature(Request.Query))
            return Ok(new { RspCode = "97", Message = "Invalid signature" });

        var txnRef = _vnPay.GetTxnRef(Request.Query);
        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.TransactionCode == txnRef, cancellationToken);

        if (payment == null)
            return Ok(new { RspCode = "01", Message = "Order not found" });

        payment.GatewayStatus = _vnPay.GetGatewayStatus(Request.Query);
        payment.GatewayPaymentLinkId = _vnPay.GetTransactionNo(Request.Query);

        if (_vnPay.IsSuccess(Request.Query))
            await _activation.MarkPaymentPaidAsync(payment, "PAID", cancellationToken);
        else
            await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { RspCode = "00", Message = "Confirm Success" });
    }

    [HttpPost("momo/ipn")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> MomoIpn(CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (!_momo.VerifySignature(root))
            return Unauthorized(new { resultCode = 97, message = "Invalid signature" });

        var orderId = _momo.GetOrderId(root);
        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.TransactionCode == orderId, cancellationToken);

        if (payment == null)
            return Ok(new { resultCode = 0, message = "Payment not found but webhook accepted" });

        payment.GatewayStatus = _momo.GetGatewayStatus(root);
        payment.GatewayPaymentLinkId = _momo.GetTransactionId(root);

        if (_momo.IsSuccess(root))
            await _activation.MarkPaymentPaidAsync(payment, "PAID", cancellationToken);
        else
            await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { resultCode = 0, message = "Success" });
    }
}
