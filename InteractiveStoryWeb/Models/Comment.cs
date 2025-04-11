namespace InteractiveStoryWeb.Models
{
    public class Comment
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int StoryId { get; set; }
        public int? ChapterId { get; set; }
        public string Content { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ApplicationUser User { get; set; }
        public Story Story { get; set; }
        public Chapter? Chapter { get; set; }
    }
}
