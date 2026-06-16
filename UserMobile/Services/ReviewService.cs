using UserMobile.Models;

namespace UserMobile.Services;

public sealed class ReviewService : IReviewService
{
    private readonly IApiService _apiService;

    public ReviewService(IApiService apiService)
    {
        _apiService = apiService;
    }

    public Task<ApiResponse<ReviewSummaryDto>> GetPoiReviewsAsync(
        string poiId,
        CancellationToken cancellationToken = default)
    {
        return _apiService.GetAsync<ReviewSummaryDto>(
            $"api/poi/{Uri.EscapeDataString(poiId)}/reviews",
            cancellationToken);
    }

    public Task<ApiResponse<ReviewSummaryDto>> SubmitPoiReviewAsync(
        string poiId,
        int rating,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        return _apiService.PostAsync<ReviewSummaryDto>(
            $"api/poi/{Uri.EscapeDataString(poiId)}/reviews",
            new SubmitReviewRequest
            {
                Rating = rating,
                Comment = comment?.Trim()
            },
            cancellationToken);
    }
}
