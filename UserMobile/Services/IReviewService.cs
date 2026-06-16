using UserMobile.Models;

namespace UserMobile.Services;

public interface IReviewService
{
    Task<ApiResponse<ReviewSummaryDto>> GetPoiReviewsAsync(string poiId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReviewSummaryDto>> SubmitPoiReviewAsync(string poiId, int rating, string? comment, CancellationToken cancellationToken = default);
}
