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
        public DbSet<ChapterSegment> ChapterSegments { get; set; }
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
        public DbSet<ReaderStoryCustomization> ReaderStoryCustomizations { get; set; }
        public DbSet<ReadingProgress> ReadingProgresses { get; set; }
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 🔸 CHAPTER → SEGMENTS
            builder.Entity<Chapter>()
                .HasMany(c => c.Segments)
                .WithOne(s => s.Chapter)
                .HasForeignKey(s => s.ChapterId)
                .OnDelete(DeleteBehavior.Cascade);

            // 🔸 SEGMENT → CHOICES
            builder.Entity<ChapterSegment>()
                .HasMany(s => s.Choices)
                .WithOne(c => c.ChapterSegment)
                .HasForeignKey(c => c.ChapterSegmentId)
                .OnDelete(DeleteBehavior.Cascade);

            // 🔸 CHOICE → CHAPTER SEGMENT
             builder.Entity<Choice>()
                .HasOne(c => c.ChapterSegment)
                .WithMany(s => s.Choices)
                .HasForeignKey(c => c.ChapterSegmentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Choice>()
                .HasOne(c => c.NextSegment)
                .WithMany()
                .HasForeignKey(c => c.NextSegmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔸 LIBRARY → STORY + USER
            builder.Entity<Library>()
                 .HasOne(l => l.Story)
                 .WithMany()
                 .HasForeignKey(l => l.StoryId)
                 .OnDelete(DeleteBehavior.Cascade);
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
                .OnDelete(DeleteBehavior.Cascade);
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
                 .OnDelete(DeleteBehavior.Cascade);
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
                .OnDelete(DeleteBehavior.SetNull);
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
            builder.Entity<Report>()
                .HasOne(r => r.Author)
                .WithMany()
                .HasForeignKey(r => r.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔸 BLOCK → STORY + BLOCKED USER + BLOCKER USER
            builder.Entity<Block>()
                .HasOne(b => b.BlockedStory)
                .WithMany()
                .HasForeignKey(b => b.BlockedStoryId)
                .OnDelete(DeleteBehavior.Cascade);
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

            // Cấu hình ReaderStoryCustomization
            builder.Entity<ReaderStoryCustomization>()
                .HasOne(rsc => rsc.User)
                .WithMany()
                .HasForeignKey(rsc => rsc.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ReaderStoryCustomization>()
                .HasOne(rsc => rsc.Story)
                .WithMany()
                .HasForeignKey(rsc => rsc.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Thêm ràng buộc khóa duy nhất trên UserId và StoryId
            builder.Entity<ReaderStoryCustomization>()
                .HasIndex(rsc => new { rsc.UserId, rsc.StoryId })
                .IsUnique();

            // 🔸 READING PROGRESS → USER + STORY + CHAPTER SEGMENT
            builder.Entity<ReadingProgress>()
                .HasOne(rp => rp.User)
                .WithMany()
                .HasForeignKey(rp => rp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ReadingProgress>()
                .HasOne(rp => rp.Story)
                .WithMany()
                .HasForeignKey(rp => rp.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ReadingProgress>()
                .HasOne(rp => rp.ChapterSegment)
                .WithMany()
                .HasForeignKey(rp => rp.ChapterSegmentId)
                .OnDelete(DeleteBehavior.Restrict); 

            // Thêm ràng buộc khóa duy nhất trên UserId và StoryId
            builder.Entity<ReadingProgress>()
                .HasIndex(rp => new { rp.UserId, rp.StoryId })
                .IsUnique();

            builder.Entity<ChapterSegment>()
                .Property(s => s.ImagePosition)
                .HasConversion<int>();
        }
    }
}
