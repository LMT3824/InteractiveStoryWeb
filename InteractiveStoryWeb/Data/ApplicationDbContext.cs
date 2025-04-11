using InteractiveStoryWeb.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InteractiveStoryWeb.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Story> Stories { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<Choice> Choices { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<Library> Libraries { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Follow> Follows { get; set; }
        public DbSet<Block> Blocks { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<Report> Reports { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            base.OnModelCreating(builder);

            // Chapter có nhiều lựa chọn đi ra (Choices), mỗi Choice thuộc 1 Chapter (gốc)
            builder.Entity<Choice>()
                .HasOne(c => c.Chapter)
                .WithMany(ch => ch.Choices)
                .HasForeignKey(c => c.ChapterId)
                .OnDelete(DeleteBehavior.Restrict);

            // Mỗi Choice dẫn đến 1 chương tiếp theo (NextChapter), không cần ngược lại
            builder.Entity<Choice>()
                .HasOne(c => c.NextChapter)
                .WithMany()
                .HasForeignKey(c => c.NextChapterId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔸 CHAPTER → PARENT CHAPTER
            builder.Entity<Chapter>()
                .HasOne<Chapter>()
                .WithMany()
                .HasForeignKey(c => c.ParentChapterId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔸 LIBRARY → STORY + USER
            builder.Entity<Library>()
                .HasOne(l => l.Story)
                .WithMany()
                .HasForeignKey(l => l.StoryId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Library>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔸 RATING → STORY + USER
            builder.Entity<Rating>()
                .HasOne(r => r.Story)
                .WithMany()
                .HasForeignKey(r => r.StoryId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Rating>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔸 COMMENT → STORY + CHAPTER + USER
            builder.Entity<Comment>()
                .HasOne(c => c.Story)
                .WithMany()
                .HasForeignKey(c => c.StoryId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Comment>()
                .HasOne(c => c.Chapter)
                .WithMany()
                .HasForeignKey(c => c.ChapterId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔸 REPORT → STORY + COMMENT + USER
            builder.Entity<Report>()
                .HasOne(r => r.Story)
                .WithMany()
                .HasForeignKey(r => r.StoryId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Report>()
                .HasOne(r => r.Comment)
                .WithMany()
                .HasForeignKey(r => r.CommentId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Report>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔸 BLOCK → STORY + BLOCKED USER + BLOCKER USER
            builder.Entity<Block>()
                .HasOne(b => b.BlockedStory)
                .WithMany()
                .HasForeignKey(b => b.BlockedStoryId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Block>()
                .HasOne(b => b.BlockedUser)
                .WithMany()
                .HasForeignKey(b => b.BlockedUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Block>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔸 FOLLOW → FOLLOWER + FOLLOWING
            builder.Entity<Follow>()
                .HasOne(f => f.Follower)
                .WithMany()
                .HasForeignKey(f => f.FollowerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Follow>()
                .HasOne(f => f.Following)
                .WithMany()
                .HasForeignKey(f => f.FollowingId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔸 SUPPORT → USER
            builder.Entity<SupportTicket>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔸 NOTIFICATION → USER
            builder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
