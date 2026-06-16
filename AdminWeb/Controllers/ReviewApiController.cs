using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Controllers.Api;

[Route("api/poi")]
[ApiController]
public sealed class ReviewApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReviewApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{poiId:int}/reviews")]
    public async Task<ActionResult<ApiResponse<ReviewSummaryDto>>> GetPoiReviews(
        int poiId,
        CancellationToken cancellationToken)
    {
        var poiExists = await _context.Pois
            .AsNoTracking()
            .AnyAsync(item => item.Id == poiId && item.Status == "Approved", cancellationToken);

        if (!poiExists)
            return NotFound(ApiResponse<ReviewSummaryDto>.Fail("POI không tồn tại hoặc chưa được duyệt."));

        var currentTouristId = TryGetTouristId();

        var reviews = await _context.PoiReviews
            .AsNoTracking()
            .Include(item => item.Tourist)
            .Where(item => item.PoiId == poiId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var data = new ReviewSummaryDto
        {
            PoiId = poiId,
            AverageRating = reviews.Count > 0 ? Math.Round(reviews.Average(item => item.Rating), 1) : 0,
            RatingCount = reviews.Count,
            MyReview = currentTouristId.HasValue
                ? reviews.Where(item => item.TouristId == currentTouristId.Value).Select(ToDto).FirstOrDefault()
                : null,
            Reviews = reviews.Select(ToDto).ToList()
        };

        return Ok(ApiResponse<ReviewSummaryDto>.Ok(data));
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("{poiId:int}/reviews")]
    public async Task<ActionResult<ApiResponse<ReviewSummaryDto>>> SubmitPoiReview(
        int poiId,
        [FromBody] SubmitReviewRequest request,
        CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();

        var poiExists = await _context.Pois
            .AsNoTracking()
            .AnyAsync(item => item.Id == poiId && item.Status == "Approved", cancellationToken);

        if (!poiExists)
            return NotFound(ApiResponse<ReviewSummaryDto>.Fail("POI không tồn tại hoặc chưa được duyệt."));

        if (request.Rating < 1 || request.Rating > 5)
            return BadRequest(ApiResponse<ReviewSummaryDto>.Fail("Số sao đánh giá phải từ 1 đến 5."));

        var comment = NormalizeComment(request.Comment);
        if ((request.Comment?.Length ?? 0) > 600)
            return BadRequest(ApiResponse<ReviewSummaryDto>.Fail("Bình luận tối đa 600 ký tự."));

        var review = await _context.PoiReviews
            .FirstOrDefaultAsync(item => item.PoiId == poiId && item.TouristId == touristId, cancellationToken);

        if (review == null)
        {
            _context.PoiReviews.Add(new PoiReview
            {
                PoiId = poiId,
                TouristId = touristId,
                Rating = request.Rating,
                Comment = comment,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            review.Rating = request.Rating;
            review.Comment = comment;
            review.CreatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var response = await BuildSummaryAsync(poiId, touristId, cancellationToken);
        return Ok(ApiResponse<ReviewSummaryDto>.Ok(response, "Cảm ơn bạn đã gửi đánh giá."));
    }

    private async Task<ReviewSummaryDto> BuildSummaryAsync(
        int poiId,
        int touristId,
        CancellationToken cancellationToken)
    {
        var reviews = await _context.PoiReviews
            .AsNoTracking()
            .Include(item => item.Tourist)
            .Where(item => item.PoiId == poiId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return new ReviewSummaryDto
        {
            PoiId = poiId,
            AverageRating = reviews.Count > 0 ? Math.Round(reviews.Average(item => item.Rating), 1) : 0,
            RatingCount = reviews.Count,
            MyReview = reviews.Where(item => item.TouristId == touristId).Select(ToDto).FirstOrDefault(),
            Reviews = reviews.Select(ToDto).ToList()
        };
    }

    private static ReviewDto ToDto(PoiReview review)
    {
        return new ReviewDto
        {
            Id = review.Id,
            PoiId = review.PoiId,
            TouristId = review.TouristId,
            TouristName = string.IsNullOrWhiteSpace(review.Tourist?.FullName)
                ? $"Du khách #{review.TouristId}"
                : review.Tourist.FullName,
            Rating = review.Rating,
            Comment = review.Comment ?? string.Empty,
            CreatedAt = review.CreatedAt
        };
    }

    private int GetTouristId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private int? TryGetTouristId()
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
    }

    private static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return null;

        var normalized = comment.Trim();
        return normalized.Length > 600 ? normalized[..600] : normalized;
    }
}
