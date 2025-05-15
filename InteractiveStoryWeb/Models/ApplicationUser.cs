using Microsoft.AspNetCore.Identity;

namespace InteractiveStoryWeb.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? AvatarUrl { get; set; }
        public string? Caption { get; set; }
        public bool IsBanned { get; set; } = false;

        // Quan hệ: User có thể có nhiều truyện
        public ICollection<Story>? Stories { get; set; }
    }
}