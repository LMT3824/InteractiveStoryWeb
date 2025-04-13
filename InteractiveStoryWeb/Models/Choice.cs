using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.Models
{
    public class Choice
    {
        public int Id { get; set; }
        public int ChapterSegmentId { get; set; }

        [Required]
        public string ChoiceText { get; set; }

        public int NextSegmentId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now; // Bỏ [Required]
        public DateTime? UpdatedAt { get; set; }

        public ChapterSegment ChapterSegment { get; set; }
        public ChapterSegment? NextSegment { get; set; }
    }
}
