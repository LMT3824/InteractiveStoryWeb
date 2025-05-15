namespace InteractiveStoryWeb.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string Title { get; set; } // Thêm Title
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false; // Thêm IsRead, mặc định là false

        public ApplicationUser? User { get; set; }
    }
}
