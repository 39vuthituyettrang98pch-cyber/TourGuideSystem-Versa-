namespace UserMobile.Models;

public sealed class ReviewDto
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public int TouristId { get; set; }
    public string TouristName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public string RatingText => $"{Rating}/5 sao";
    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}

public sealed class ReviewSummaryDto
{
    public int PoiId { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public ReviewDto? MyReview { get; set; }
    public List<ReviewDto> Reviews { get; set; } = [];

    public string SummaryText => RatingCount > 0
        ? $"{AverageRating:F1}/5 · {RatingCount} đánh giá"
        : "Chưa có đánh giá";
}

public sealed class SubmitReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}
