using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.Models
{
    public class Chapter
    {
        public int Id { get; set; }
        public int StoryId { get; set; }

        [Required]
        public string Title { get; set; } // Tiêu đề chương

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public bool IsPublic { get; set; } = false;
        public int ViewCount { get; set; } = 0;

        public Story? Story { get; set; }

        public ICollection<ChapterSegment> Segments { get; set; } = new List<ChapterSegment>(); // Nhiều đoạn nhỏ
    }
}
