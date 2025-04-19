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
        public async Task<IActionResult> MyProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            var stories = await _context.Stories
                .Where(s => s.AuthorId == user.Id)
                .Include(s => s.Chapters)
                .ToListAsync();

            // Tính tổng ViewCount cho từng Story
            var viewCounts = new Dictionary<int, int>();
            foreach (var story in stories)
            {
                viewCounts[story.Id] = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;
            }

            // Lấy tiến trình đọc của người dùng
            var readingProgresses = await _context.ReadingProgresses
                .Where(rp => rp.UserId == user.Id)
                .Include(rp => rp.Story)
                .Include(rp => rp.ChapterSegment)
                    .ThenInclude(cs => cs.Chapter)
                .OrderByDescending(rp => rp.LastReadAt)
                .ToListAsync();

            ViewBag.ViewCounts = viewCounts;
            ViewBag.User = user;
            ViewBag.ReadingProgresses = readingProgresses;

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
    }
}
