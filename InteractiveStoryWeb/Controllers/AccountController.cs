using InteractiveStoryWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InteractiveStoryWeb.Models;
using InteractiveStoryWeb.ViewModels;

namespace InteractiveStoryWeb.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger _logger;

        public AccountController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<AccountController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [Authorize]
        public async Task<IActionResult> MyProfile(string userId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            string currentUserId = currentUser.Id; // Khai báo currentUserId

            // Xác định người dùng cần hiển thị hồ sơ
            ApplicationUser user;
            bool isOwnProfile;
            if (string.IsNullOrEmpty(userId) || userId == currentUser.Id)
            {
                user = currentUser;
                isOwnProfile = true;
            }
            else
            {
                user = await _userManager.FindByIdAsync(userId);
                isOwnProfile = false;
            }

            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            // Kiểm tra xem người dùng hiện tại có chặn người này không
            bool isBlocked = false;
            if (!isOwnProfile)
            {
                isBlocked = await _context.Blocks
                    .AnyAsync(b => b.UserId == currentUser.Id && b.BlockedUserId == user.Id);
            }

            // Lấy danh sách truyện, loại bỏ truyện bị chặn
            var stories = await _context.Stories
                .Where(s => s.AuthorId == user.Id && !s.Author.IsBanned
                    && (isOwnProfile || (s.IsPublic && !s.IsHidden)) // Cho phép tác giả thấy truyện bị ẩn
                    && (currentUserId == null || !_context.Blocks.Any(b => b.UserId == currentUserId && b.BlockedStoryId == s.Id)))
                .Include(s => s.Chapters)
                .ToListAsync();

            // Tính tổng ViewCount, Ratings, và ChapterCount
            var viewCounts = new Dictionary<int, int>();
            var ratings = new Dictionary<int, double>();
            var chapterCounts = new Dictionary<int, int>();
            foreach (var story in stories)
            {
                viewCounts[story.Id] = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;
                var storyRatings = await _context.Ratings
                    .Where(r => r.StoryId == story.Id && !r.User.IsBanned)
                    .ToListAsync();
                ratings[story.Id] = storyRatings.Any() ? storyRatings.Average(r => r.RatingValue) : 0;
                chapterCounts[story.Id] = story.Chapters?.Count ?? 0;
            }

            // Đếm số lượng Following, loại bỏ người bị chặn và người chặn mình
            var followingCount = await _context.Follows
                .Join(_context.ApplicationUsers,
                      f => f.FollowingId,
                      u => u.Id,
                      (f, u) => new { Follow = f, User = u })
                .Where(fu => fu.Follow.FollowerId == user.Id && !fu.User.IsBanned
                    && !_context.Blocks.Any(b => b.UserId == currentUserId && b.BlockedUserId == fu.Follow.FollowingId)
                    && !_context.Blocks.Any(b => b.UserId == fu.Follow.FollowingId && b.BlockedUserId == currentUserId))
                .CountAsync();

            // Đếm số lượng Followers, loại bỏ người bị chặn và người chặn mình
            var followersCount = await _context.Follows
                .Join(_context.ApplicationUsers,
                      f => f.FollowerId,
                      u => u.Id,
                      (f, u) => new { Follow = f, User = u })
                .Where(fu => fu.Follow.FollowingId == user.Id && !fu.User.IsBanned
                    && !_context.Blocks.Any(b => b.UserId == currentUserId && b.BlockedUserId == fu.Follow.FollowerId)
                    && !_context.Blocks.Any(b => b.UserId == fu.Follow.FollowerId && b.BlockedUserId == currentUserId))
                .CountAsync();

            var storiesCount = stories.Count; // Sử dụng thuộc tính Count của List<Story>

            bool isFollowing = false;
            if (!isOwnProfile)
            {
                isFollowing = await _context.Follows
                    .AnyAsync(f => f.FollowerId == currentUser.Id && f.FollowingId == user.Id);
            }

            List<ReadingProgress> readingProgresses = null;
            if (isOwnProfile)
            {
                readingProgresses = await _context.ReadingProgresses
                    .Where(rp => rp.UserId == user.Id)
                    .Include(rp => rp.Story)
                    .Include(rp => rp.ChapterSegment)
                        .ThenInclude(cs => cs.Chapter)
                    .OrderByDescending(rp => rp.LastReadAt)
                    .ToListAsync();
            }

            ViewBag.ViewCounts = viewCounts;
            ViewBag.Ratings = ratings;
            ViewBag.ChapterCounts = chapterCounts;
            ViewBag.User = user;
            ViewBag.ReadingProgresses = readingProgresses;
            ViewBag.FollowingCount = followingCount;
            ViewBag.FollowersCount = followersCount;
            ViewBag.StoriesCount = storiesCount;
            ViewBag.IsOwnProfile = isOwnProfile;
            ViewBag.IsFollowing = isFollowing;
            ViewBag.IsBlocked = isBlocked;

            return View(stories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> MyProfile(IFormFile avatarFile, string Caption)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            // Validation: Giới hạn Caption tối đa 200 ký tự
            if (!string.IsNullOrEmpty(Caption) && Caption.Length > 210)
            {
                return Json(new { success = false, message = "Giới thiệu không được vượt quá 200 ký tự." });
            }

            if (avatarFile != null && avatarFile.Length > 0)
            {
                if (avatarFile.Length > 2 * 1024 * 1024) // Giới hạn 2MB
                {
                    return Json(new { success = false, message = "Ảnh đại diện không được vượt quá 2MB." });
                }

                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/avatars");
                Directory.CreateDirectory(uploads);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(stream);
                }

                // Xóa ảnh cũ nếu có
                if (!string.IsNullOrEmpty(user.AvatarUrl))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.AvatarUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                user.AvatarUrl = "/uploads/avatars/" + fileName;
            }

            user.Caption = Caption;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return Json(new { success = true, avatarUrl = user.AvatarUrl, caption = user.Caption });
            }
            else
            {
                return Json(new { success = false, message = "Có lỗi xảy ra khi cập nhật hồ sơ." });
            }
        }

        [Authorize]
        public async Task<IActionResult> Following(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var following = await _context.Follows
                .Where(f => f.FollowerId == userId && !f.Following.IsBanned
                    && !_context.Blocks.Any(b => b.UserId == currentUser.Id && b.BlockedUserId == f.FollowingId)
                    && !_context.Blocks.Any(b => b.UserId == f.FollowingId && b.BlockedUserId == currentUser.Id))
                .Include(f => f.Following)
                .Select(f => f.Following)
                .ToListAsync();

            var isFollowingDict = new Dictionary<string, bool>();
            if (currentUser != null)
            {
                foreach (var followedUser in following)
                {
                    bool isFollowing = await _context.Follows
                        .AnyAsync(f => f.FollowerId == currentUser.Id && f.FollowingId == followedUser.Id);
                    isFollowingDict[followedUser.Id] = isFollowing;
                }
            }

            ViewBag.TargetUser = user;
            ViewBag.IsFollowing = isFollowingDict;
            return View(following);
        }

        [Authorize]
        public async Task<IActionResult> Followers(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var followers = await _context.Follows
                .Where(f => f.FollowingId == userId && !f.Follower.IsBanned
                    && !_context.Blocks.Any(b => b.UserId == currentUser.Id && b.BlockedUserId == f.FollowerId)
                    && !_context.Blocks.Any(b => b.UserId == f.FollowerId && b.BlockedUserId == currentUser.Id))
                .Include(f => f.Follower)
                .Select(f => f.Follower)
                .ToListAsync();

            var isFollowingDict = new Dictionary<string, bool>();
            if (currentUser != null)
            {
                foreach (var follower in followers)
                {
                    bool isFollowing = await _context.Follows
                        .AnyAsync(f => f.FollowerId == currentUser.Id && f.FollowingId == follower.Id);
                    isFollowingDict[follower.Id] = isFollowing;
                }
            }

            ViewBag.TargetUser = user;
            ViewBag.IsFollowing = isFollowingDict;
            return View(followers);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> FollowUser(string targetUserId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            if (currentUser.Id == targetUserId)
            {
                return Json(new { success = false, message = "Bạn không thể theo dõi chính mình." });
            }

            var targetUser = await _userManager.FindByIdAsync(targetUserId);
            if (targetUser == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            // Kiểm tra xem đã theo dõi chưa
            var existingFollow = await _context.Follows
                .FirstOrDefaultAsync(f => f.FollowerId == currentUser.Id && f.FollowingId == targetUserId);

            if (existingFollow != null)
            {
                return Json(new { success = false, message = "Bạn đã theo dõi người dùng này." });
            }

            var follow = new Follow
            {
                FollowerId = currentUser.Id,
                FollowingId = targetUserId
            };

            _context.Follows.Add(follow);
            await _context.SaveChangesAsync();

            // Cập nhật số lượng followers
            var followersCount = await _context.Follows
                .CountAsync(f => f.FollowingId == targetUserId);

            return Json(new { success = true, message = "Đã theo dõi!", followersCount = followersCount });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UnfollowUser(string targetUserId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var targetUser = await _userManager.FindByIdAsync(targetUserId);
            if (targetUser == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var follow = await _context.Follows
                .FirstOrDefaultAsync(f => f.FollowerId == currentUser.Id && f.FollowingId == targetUserId);

            if (follow == null)
            {
                return Json(new { success = false, message = "Bạn chưa theo dõi người dùng này." });
            }

            _context.Follows.Remove(follow);
            await _context.SaveChangesAsync();

            // Cập nhật số lượng followers
            var followersCount = await _context.Follows
                .CountAsync(f => f.FollowingId == targetUserId);

            return Json(new { success = true, message = "Đã bỏ theo dõi!", followersCount = followersCount });
        }

        public async Task<IActionResult> Notifications()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("User not found for marking notifications as read.");
                    return View(new List<Notification>());
                }

                var notifications = await _context.Notifications
                    .Where(n => n.UserId == null)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                _logger.LogInformation($"Found {notifications.Count} notifications for user {user.Id}.");

                if (notifications.Any())
                {
                    foreach (var notification in notifications)
                    {
                        var userNotificationRead = await _context.UserNotificationReads
                            .FirstOrDefaultAsync(unr => unr.UserId == user.Id && unr.NotificationId == notification.Id);

                        if (userNotificationRead != null && !userNotificationRead.IsRead)
                        {
                            userNotificationRead.IsRead = true;
                            _context.Update(userNotificationRead);
                            _logger.LogInformation($"Marked notification ID {notification.Id} as read for user {user.Id}.");
                        }
                    }
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Changes saved to database.");
                }
                else
                {
                    _logger.LogWarning("No notifications found to mark as read.");
                }

                return View(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while marking notifications as read.");
                return View(new List<Notification>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadNotificationCount()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(0);
                }

                var count = await (from n in _context.Notifications
                                   join unr in _context.UserNotificationReads
                                   on n.Id equals unr.NotificationId
                                   where n.UserId == null && unr.UserId == user.Id && !unr.IsRead
                                   select n).CountAsync();

                return Json(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting unread notification count.");
                return Json(0);
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockUser(string blockedUserId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            if (currentUser.Id == blockedUserId)
            {
                return Json(new { success = false, message = "Bạn không thể chặn chính mình." });
            }

            var blockedUser = await _userManager.FindByIdAsync(blockedUserId);
            if (blockedUser == null)
            {
                return Json(new { success = false, message = "Người dùng cần chặn không tồn tại." });
            }

            var existingBlock = await _context.Blocks
                .FirstOrDefaultAsync(b => b.UserId == currentUser.Id && b.BlockedUserId == blockedUserId);

            if (existingBlock != null)
            {
                return Json(new { success = false, message = "Bạn đã chặn người dùng này." });
            }

            var block = new Block
            {
                UserId = currentUser.Id,
                BlockedUserId = blockedUserId
            };

            _context.Blocks.Add(block);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã chặn người dùng!" });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnblockUser(string blockedUserId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var block = await _context.Blocks
                .FirstOrDefaultAsync(b => b.UserId == currentUser.Id && b.BlockedUserId == blockedUserId);

            if (block == null)
            {
                return Json(new { success = false, message = "Bạn chưa chặn người dùng này." });
            }

            _context.Blocks.Remove(block);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã bỏ chặn người dùng!" });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockStory(int storyId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var story = await _context.Stories.FindAsync(storyId);
            if (story == null)
            {
                return Json(new { success = false, message = "Truyện không tồn tại." });
            }

            var existingBlock = await _context.Blocks
                .FirstOrDefaultAsync(b => b.UserId == currentUser.Id && b.BlockedStoryId == storyId);

            if (existingBlock != null)
            {
                return Json(new { success = false, message = "Bạn đã chặn truyện này." });
            }

            var block = new Block
            {
                UserId = currentUser.Id,
                BlockedStoryId = storyId
            };

            _context.Blocks.Add(block);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã chặn truyện!" });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnblockStory(int storyId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var block = await _context.Blocks
                .FirstOrDefaultAsync(b => b.UserId == currentUser.Id && b.BlockedStoryId == storyId);

            if (block == null)
            {
                return Json(new { success = false, message = "Bạn chưa chặn truyện này." });
            }

            _context.Blocks.Remove(block);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã bỏ chặn truyện!" });
        }

        [Authorize]
        public async Task<IActionResult> BlockedList()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            var blockedItems = await _context.Blocks
                .Where(b => b.UserId == currentUser.Id)
                .Include(b => b.BlockedUser)
                .Include(b => b.BlockedStory)
                .ToListAsync();

            var blockedUsers = blockedItems
                .Where(b => b.BlockedUserId != null)
                .Select(b => new BlockedItemViewModel
                {
                    Id = b.Id,
                    ItemId = b.BlockedUserId,
                    Type = "User",
                    Name = b.BlockedUser.UserName,
                    ImageUrl = b.BlockedUser.AvatarUrl ?? "/images/AvatarNull.jpg"
                })
                .ToList();

            var blockedStories = blockedItems
                .Where(b => b.BlockedStoryId != null)
                .Select(b => new BlockedItemViewModel
                {
                    Id = b.Id,
                    ItemId = b.BlockedStoryId.ToString(),
                    Type = "Story",
                    Name = b.BlockedStory.Title,
                    ImageUrl = b.BlockedStory.CoverImageUrl ?? "/images/ImageNotFound.png"
                })
                .ToList();

            var viewModel = new BlockedListViewModel
            {
                BlockedUsers = blockedUsers,
                BlockedStories = blockedStories
            };

            return View(viewModel);
        }

        [Authorize]
        public IActionResult CreateSupportTicket()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSupportTicket(string content)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Người dùng không tồn tại.";
                return RedirectToAction("MyProfile");
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Nội dung yêu cầu không được để trống.";
                return View();
            }

            if (content.Length > 1000)
            {
                TempData["ErrorMessage"] = "Nội dung yêu cầu không được vượt quá 1000 ký tự.";
                return View();
            }

            try
            {
                var supportTicket = new SupportTicket
                {
                    UserId = user.Id,
                    Content = content,
                    CreatedAt = DateTime.Now,
                    IsResolved = false
                };

                _context.SupportTickets.Add(supportTicket);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Yêu cầu hỗ trợ đã được gửi thành công!";
                return RedirectToAction("MyProfile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi yêu cầu hỗ trợ.");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi gửi yêu cầu hỗ trợ.";
                return View();
            }
        }

        [Authorize]
        public async Task<IActionResult> MySupportTickets()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            var supportTickets = await _context.SupportTickets
                .Where(st => st.UserId == user.Id)
                .Include(st => st.User)
                .Include(st => st.SupportTicketResponses) // Tải các phản hồi
                    .ThenInclude(str => str.Admin) // Tải thông tin admin
                .OrderByDescending(st => st.CreatedAt)
                .ToListAsync();

            return View(supportTickets);
        }
    }
}
