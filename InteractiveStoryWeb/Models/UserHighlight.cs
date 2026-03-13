using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.Models
{
    public class UserHighlight
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int ChapterSegmentId { get; set; }

        [Required]
        public string HighlightedText { get; set; } // Đoạn text được highlight

        // Thêm 2 cột này để định vị chính xác
        public string? ContextBefore { get; set; } // 50 ký tự trước
        public string? ContextAfter { get; set; }  // 50 ký tự sau

        [Required]
        public int StartOffset { get; set; } // Vị trí bắt đầu trong nội dung

        [Required]
        public int EndOffset { get; set; } // Vị trí kết thúc

        [Required]
        public string Color { get; set; } // Màu highlight: yellow, green, blue, pink

        public string? Note { get; set; } // Ghi chú của người đọc (optional)

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public ApplicationUser User { get; set; }
        public ChapterSegment ChapterSegment { get; set; }
    }
}
