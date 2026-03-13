using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.ViewModels
{
    public class HighlightViewModel
    {
        public int? Id { get; set; }
        public int ChapterSegmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn đoạn text để highlight.")]
        public string HighlightedText { get; set; }

        public string? ContextBefore { get; set; }
        public string? ContextAfter { get; set; } 

        public int StartOffset { get; set; }
        public int EndOffset { get; set; }

        [Required]
        public string Color { get; set; } = "yellow";

        [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
        public string? Note { get; set; }
    }
}
