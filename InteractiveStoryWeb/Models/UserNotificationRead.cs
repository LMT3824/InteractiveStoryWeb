namespace InteractiveStoryWeb.Models
{
    public class UserNotificationRead
    {
        public string UserId { get; set; }
        public int NotificationId { get; set; }
        public bool IsRead { get; set; } = false;

        public ApplicationUser User { get; set; }
        public Notification Notification { get; set; }
    }
}
