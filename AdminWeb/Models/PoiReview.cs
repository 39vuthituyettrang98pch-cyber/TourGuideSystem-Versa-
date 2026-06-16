using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdminWeb.Models;

[Table("PoiReviews")]
public class PoiReview
{
    [Key]
    public int Id { get; set; }

    public int PoiId { get; set; }
    public int TouristId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Poi? Poi { get; set; }
    public Tourist? Tourist { get; set; }
}