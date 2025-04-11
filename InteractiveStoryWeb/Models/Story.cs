namespace InteractiveStoryWeb.Models
{
    public class Story
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string AuthorId { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? Genre { get; set; }
        public bool IsPublic { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int ViewCount { get; set; } = 0;

        public ApplicationUser Author { get; set; }

        public ICollection<Chapter>? Chapters { get; set; }
    }
}
