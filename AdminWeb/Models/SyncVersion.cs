using System;
using System.ComponentModel.DataAnnotations;

namespace AdminWeb.Models
{
    public class SyncVersion
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập phiên bản")]
        [Display(Name = "Phiên bản")]
        public string VersionNumber { get; set; } = string.Empty;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}