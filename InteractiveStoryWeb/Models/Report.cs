namespace InteractiveStoryWeb.Models
{
    public class Report
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int? StoryId { get; set; }
        public int? CommentId { get; set; }
        public string AuthorId { get; set; }
        public string Reason { get; set; }
        public DateTime ReportedAt { get; set; } = DateTime.Now;

        public ApplicationUser User { get; set; }
        public Story? Story { get; set; }
        public Comment? Comment { get; set; }
        public ApplicationUser Author { get; set; }
    }
}
