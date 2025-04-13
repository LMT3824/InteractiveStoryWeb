using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.Models
{
    public class ChapterSegment
    {
        public int Id { get; set; }
        public int ChapterId { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now; // Bỏ [Required]
        public DateTime? UpdatedAt { get; set; }

        public Chapter Chapter { get; set; }
        public ICollection<Choice> Choices { get; set; } = new List<Choice>();
    }
}
