using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.Models
{
    public class Story
    {
        public int Id { get; set; }
        [Required]
        public string Title { get; set; }
        [Required]
        public string Description { get; set; }
        public string AuthorId { get; set; } 
        public string? CoverImageUrl { get; set; }

        [Required]
        public string? Genre { get; set; }
        public bool IsPublic { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public bool IsCompleted { get; set; }

        public ApplicationUser Author { get; set; }
        public ICollection<Chapter>? Chapters { get; set; } = new List<Chapter>();
    }
}
