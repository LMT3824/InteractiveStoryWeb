namespace InteractiveStoryWeb.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ApplicationUser? User { get; set; }
    }
}
