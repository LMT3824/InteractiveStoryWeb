namespace InteractiveStoryWeb.Models
{
    public class Block
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int? BlockedStoryId { get; set; }
        public string? BlockedUserId { get; set; }

        public ApplicationUser User { get; set; }
        public Story? BlockedStory { get; set; }
        public ApplicationUser? BlockedUser { get; set; }
    }
}
