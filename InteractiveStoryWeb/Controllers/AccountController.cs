using InteractiveStoryWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InteractiveStoryWeb.Models;

namespace InteractiveStoryWeb.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [Authorize]
        public async Task<IActionResult> MyProfile(string userId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

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

            var stories = await _context.Stories
                .Where(s => s.AuthorId == user.Id)
                .Include(s => s.Chapters)
                .ToListAsync();

            // Tính tổng ViewCount, Ratings, và ChapterCount cho từng Story
            var viewCounts = new Dictionary<int, int>();
            var ratings = new Dictionary<int, double>();
            var chapterCounts = new Dictionary<int, int>();
            foreach (var story in stories)
            {
                viewCounts[story.Id] = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;
                var storyRatings = await _context.Ratings.Where(r => r.StoryId == story.Id).ToListAsync();
                ratings[story.Id] = storyRatings.Any() ? storyRatings.Average(r => r.RatingValue) : 0;
                chapterCounts[story.Id] = story.Chapters?.Count ?? 0;
            }

            // Đếm số lượng Following, Followers và số lượng tác phẩm
            var followingCount = await _context.Follows
                .CountAsync(f => f.FollowerId == user.Id);
            var followersCount = await _context.Follows
                .CountAsync(f => f.FollowingId == user.Id);
            var storiesCount = stories.Count; // Số lượng truyện đã đăng (tác phẩm)

            // Kiểm tra xem người dùng hiện tại có đang theo dõi người này không (nếu không phải trang cá nhân của họ)
            bool isFollowing = false;
            if (!isOwnProfile)
            {
                isFollowing = await _context.Follows
                    .AnyAsync(f => f.FollowerId == currentUser.Id && f.FollowingId == user.Id);
            }

            // Lấy tiến trình đọc của người dùng (chỉ hiển thị trên trang cá nhân của họ)
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
            ViewBag.StoriesCount = storiesCount; // Thêm số lượng tác phẩm vào ViewBag
            ViewBag.IsOwnProfile = isOwnProfile;
            ViewBag.IsFollowing = isFollowing;

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
            if (!string.IsNullOrEmpty(Caption) && Caption.Length > 200)
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

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> EditProfile(ApplicationUser model, IFormFile avatarFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            if (avatarFile != null && avatarFile.Length > 0)
            {
                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/avatars");
                Directory.CreateDirectory(uploads);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(stream);
                }

                user.AvatarUrl = "/uploads/avatars/" + fileName;
            }

            user.Caption = model.Caption;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Hồ sơ đã được cập nhật thành công!";
                return RedirectToAction("MyProfile");
            }
            else
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật hồ sơ.";
                return View(user);
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

            var following = await _context.Follows
                .Where(f => f.FollowerId == userId)
                .Include(f => f.Following)
                .Select(f => f.Following)
                .ToListAsync();

            // Kiểm tra trạng thái theo dõi của currentUser đối với từng người trong danh sách
            var currentUser = await _userManager.GetUserAsync(User);
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

            var followers = await _context.Follows
                .Where(f => f.FollowingId == userId)
                .Include(f => f.Follower)
                .Select(f => f.Follower)
                .ToListAsync();

            // Kiểm tra trạng thái theo dõi của currentUser đối với từng người trong danh sách
            var currentUser = await _userManager.GetUserAsync(User);
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

        
    }
}
