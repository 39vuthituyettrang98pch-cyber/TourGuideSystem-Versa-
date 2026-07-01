using AdminWeb.Areas.Owner.ViewModels;
using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Policy = "OwnerAreaPolicy")]
public sealed class DashboardController : Controller
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var poiIds = await _context.Pois
            .Where(poi => poi.OwnerProfileId == owner.Id)
            .Select(poi => poi.Id)
            .ToListAsync(cancellationToken);

        await NormalizeCompletedRequestsAsync(owner.Id, cancellationToken);

        ViewBag.PoiCount = poiIds.Count;
        ViewBag.ReviewCount = await _context.PoiReviews.CountAsync(review => poiIds.Contains(review.PoiId), cancellationToken);
        ViewBag.PlayCount = await _context.VisitorPlaybackLogs.CountAsync(log => poiIds.Contains(log.PoiId), cancellationToken);
        ViewBag.PendingPayments = await _context.PaymentTransactions.CountAsync(payment => payment.OwnerProfileId == owner.Id && payment.Status == "Pending", cancellationToken);
        ViewBag.MenuItemCount = await _context.OwnerMenuItems.CountAsync(item => item.OwnerProfileId == owner.Id, cancellationToken);
        ViewBag.PendingRequestCount = await _context.PoiOwnerRequests.CountAsync(item => item.OwnerProfileId == owner.Id && item.Status == "Pending", cancellationToken);
        var activeSubscription = await _context.OwnerSubscriptions
            .Include(item => item.PaymentPlan)
            .Where(item => item.OwnerProfileId == owner.Id && item.Status == "Active")
            .OrderByDescending(item => item.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);
        ViewBag.ActiveSubscription = activeSubscription;

        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        var newOrderCount = await _context.MenuOrders.CountAsync(order =>
            order.OwnerProfileId == owner.Id &&
            (order.Status == "Pending" || order.Status == "Confirmed" || order.Status == "Preparing"),
            cancellationToken);

        var todayOrderCount = await _context.MenuOrders.CountAsync(order =>
            order.OwnerProfileId == owner.Id &&
            order.CreatedAt >= todayStart &&
            order.CreatedAt < todayEnd,
            cancellationToken);

        var todayRevenue = await _context.MenuOrders
            .Where(order =>
                order.OwnerProfileId == owner.Id &&
                order.CreatedAt >= todayStart &&
                order.CreatedAt < todayEnd &&
                (order.PaymentStatus == "Paid" || order.Status == "Completed"))
            .Select(order => order.TotalAmount)
            .ToListAsync(cancellationToken);

        var newReviewCount = await _context.PoiReviews.CountAsync(review =>
            poiIds.Contains(review.PoiId) &&
            review.CreatedAt >= sevenDaysAgo,
            cancellationToken);

        ViewBag.NewOrderCount = newOrderCount;
        ViewBag.TodayOrderCount = todayOrderCount;
        ViewBag.TodayRevenue = todayRevenue.Sum();
        ViewBag.NewReviewCount = newReviewCount;

        var notifications = new List<OwnerDashboardNotificationViewModel>();

        if (newOrderCount > 0)
        {
            notifications.Add(new OwnerDashboardNotificationViewModel
            {
                Icon = "fa-solid fa-receipt",
                Title = $"Có {newOrderCount} đơn hàng cần xử lý",
                Message = "Kiểm tra đơn mới, xác nhận hoặc cập nhật trạng thái chuẩn bị cho khách.",
                Level = "warning",
                ActionText = "Xem đơn hàng",
                ActionUrl = Url.Action("Index", "MenuOrders", new { area = "Owner" }),
                CreatedAt = DateTime.Now
            });
        }

        if (newReviewCount > 0)
        {
            notifications.Add(new OwnerDashboardNotificationViewModel
            {
                Icon = "fa-solid fa-star-half-stroke",
                Title = $"Có {newReviewCount} đánh giá mới trong 7 ngày",
                Message = "Theo dõi phản hồi của du khách để cải thiện dịch vụ và nội dung gian hàng.",
                Level = "info",
                ActionText = "Xem đánh giá",
                ActionUrl = Url.Action("Index", "Reviews", new { area = "Owner" }),
                CreatedAt = DateTime.Now
            });
        }

        if (activeSubscription != null)
        {
            var daysLeft = (activeSubscription.ExpiresAt.ToUniversalTime().Date - DateTime.UtcNow.Date).Days;
            if (daysLeft <= 7)
            {
                notifications.Add(new OwnerDashboardNotificationViewModel
                {
                    Icon = "fa-solid fa-crown",
                    Title = daysLeft < 0 ? "Gói nổi bật đã hết hạn" : $"Gói nổi bật còn {Math.Max(daysLeft, 0)} ngày",
                    Message = "Gia hạn gói để gian hàng tiếp tục được ưu tiên hiển thị trên bản đồ và danh sách khám phá.",
                    Level = daysLeft < 0 ? "danger" : "warning",
                    ActionText = "Gia hạn gói",
                    ActionUrl = Url.Action("Index", "Payments", new { area = "Owner" }),
                    CreatedAt = DateTime.Now
                });
            }
        }
        else
        {
            notifications.Add(new OwnerDashboardNotificationViewModel
            {
                Icon = "fa-solid fa-bolt",
                Title = "Chưa có gói nổi bật đang hoạt động",
                Message = "Mua gói nổi bật để POI/gian hàng được ưu tiên hiển thị cho du khách.",
                Level = "info",
                ActionText = "Mua gói",
                ActionUrl = Url.Action("Index", "Payments", new { area = "Owner" }),
                CreatedAt = DateTime.Now
            });
        }

        if (todayRevenue.Sum() > 0)
        {
            notifications.Add(new OwnerDashboardNotificationViewModel
            {
                Icon = "fa-solid fa-chart-line",
                Title = $"Doanh thu hôm nay {todayRevenue.Sum():N0} VND",
                Message = $"Hôm nay có {todayOrderCount} đơn menu được ghi nhận tại gian hàng.",
                Level = "success",
                ActionText = "Xem báo cáo",
                ActionUrl = Url.Action("Index", "Reports", new { area = "Owner" }),
                CreatedAt = DateTime.Now
            });
        }

        ViewBag.Notifications = notifications;

        return View(owner);
    }

    private async Task NormalizeCompletedRequestsAsync(int ownerId, CancellationToken cancellationToken)
    {
        var completedRequests = await _context.PoiOwnerRequests
            .Include(item => item.Poi)
            .Where(item =>
                item.OwnerProfileId == ownerId &&
                item.Status == "Pending" &&
                item.Poi != null &&
                item.Poi.OwnerProfileId == ownerId &&
                item.Poi.Status == "Approved")
            .ToListAsync(cancellationToken);

        if (completedRequests.Count == 0)
            return;

        var reviewedAt = DateTime.UtcNow;
        foreach (var request in completedRequests)
        {
            request.Status = "Approved";
            request.ReviewedAt ??= reviewedAt;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<AdminWeb.Models.OwnerProfile?> GetOwnerAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.Identity?.Name;

        return await _context.OwnerProfiles
            .Include(owner => owner.User)
            .FirstOrDefaultAsync(owner =>
                (userId != null && owner.UserId.ToString() == userId) ||
                owner.User!.Username == username,
                cancellationToken);
    }
}
