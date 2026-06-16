using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdminWeb.Models;

[Table("TouristBookmarks")]
public class TouristBookmark
{
    [Key]
    public int Id { get; set; }

    public int TouristId { get; set; }
    public int PoiId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("TouristId")]
    public Tourist? Tourist { get; set; }

    [ForeignKey("PoiId")]
    public Poi? Poi { get; set; }
}