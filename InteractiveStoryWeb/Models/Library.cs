namespace InteractiveStoryWeb.Models
{
    public class Library
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int StoryId { get; set; }

        public ApplicationUser User { get; set; }
        public Story Story { get; set; }
    }
}
