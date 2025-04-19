namespace InteractiveStoryWeb.Models
{
    public class ReadingProgress
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int StoryId { get; set; }
        public int? ChapterSegmentId { get; set; }
        public DateTime LastReadAt { get; set; } = DateTime.Now;

        public ApplicationUser User { get; set; }
        public Story Story { get; set; }
        public ChapterSegment? ChapterSegment { get; set; }
    }
}
