namespace InteractiveStoryWeb.Models
{
    public class Chapter
    {
        public int Id { get; set; }
        public int StoryId { get; set; }
        public string Content { get; set; }
        public string? ImageUrl { get; set; }
        public int? ParentChapterId { get; set; }
        public DateTime CreatedAt { get; set; }

        public Story Story { get; set; }
        public ICollection<Choice>? Choices { get; set; }
    }
}
