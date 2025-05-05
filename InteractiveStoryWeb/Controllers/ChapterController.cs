using InteractiveStoryWeb.Data;
using InteractiveStoryWeb.Models;
using InteractiveStoryWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InteractiveStoryWeb.Controllers
{
    [Authorize]
    public class ChapterController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public ChapterController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ChapterCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin.";
                return View(model);
            }

            string imagePath = null;

            if (model.Image != null && model.Image.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads/segments");
                Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.Image.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Image.CopyToAsync(stream);
                }

                imagePath = "/uploads/segments/" + fileName;
            }

            var chapter = new Chapter
            {
                StoryId = model.StoryId,
                Title = model.Title,
                IsPublic = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.Chapters.Add(chapter);
            await _context.SaveChangesAsync();

            // Tạo đoạn 1 với giá trị mặc định
            var firstSegment = new ChapterSegment
            {
                ChapterId = chapter.Id,
                Title = "Đoạn 1",
                Content = "Đoạn mới được tạo tự động.",
                ImageUrl = imagePath,
                ImagePosition = ImagePosition.Bottom, // Mặc định ở cuối trang
                CreatedAt = DateTime.Now
            };
            _context.ChapterSegments.Add(firstSegment);
            await _context.SaveChangesAsync();

            // Tạo đoạn 2 với giá trị mặc định
            var secondSegment = new ChapterSegment
            {
                ChapterId = chapter.Id,
                Title = "Đoạn 2",
                Content = "Đoạn mới được tạo tự động.",
                CreatedAt = DateTime.Now
            };
            _context.ChapterSegments.Add(secondSegment);
            await _context.SaveChangesAsync();

            // Tạo lựa chọn dẫn từ đoạn 1 đến đoạn 2
            var choice = new Choice
            {
                ChapterSegmentId = firstSegment.Id,
                ChoiceText = "Lựa chọn tới đoạn 2",
                NextSegmentId = secondSegment.Id,
                CreatedAt = DateTime.Now
            };
            _context.Choices.Add(choice);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Chương đã được tạo thành công!";
            return RedirectToAction("Manage", "Chapter", new { storyId = chapter.StoryId });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var chapter = await _context.Chapters.FindAsync(id);
            if (chapter == null) return NotFound();

            return RedirectToAction("Manage", new { storyId = chapter.StoryId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ChapterCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin: " + string.Join("; ", errors);
                return RedirectToAction("Manage", new { storyId = model.StoryId });
            }

            var chapter = await _context.Chapters.FindAsync(model.Id);
            if (chapter == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var story = await _context.Stories.FindAsync(chapter.StoryId);
            if (story == null || story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa chương này.";
                return RedirectToAction("Manage", new { storyId = chapter.StoryId });
            }

            // Log giá trị IsPublic để debug
            Console.WriteLine($"IsPublic from form: {model.IsPublic}");

            // Kiểm tra nếu chương được set thành không công khai
            if (!model.IsPublic && chapter.IsPublic)
            {
                // Kiểm tra xem truyện có còn chương công khai nào khác không
                var hasOtherPublicChapters = await _context.Chapters
                    .Include(c => c.Segments)
                    .AnyAsync(c => c.StoryId == story.Id && c.Id != chapter.Id && c.IsPublic && c.Segments.Any());

                if (!hasOtherPublicChapters && story.IsPublic)
                {
                    // Nếu không còn chương công khai nào, set truyện thành không công khai
                    story.IsPublic = false;
                    await _context.SaveChangesAsync();
                }
            }

            chapter.Title = model.Title;
            chapter.IsPublic = model.IsPublic;
            chapter.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Chương đã được cập nhật thành công!";
            return RedirectToAction("Manage", new { storyId = chapter.StoryId });
        }


        [AllowAnonymous]
        public async Task<IActionResult> Read(int id)
        {
            var chapter = await _context.Chapters
                .Include(c => c.Story)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (chapter == null)
                return NotFound("Chương không tồn tại.");

            if (!chapter.IsPublic)
                return NotFound("Chương không công khai.");

            // Kiểm tra tùy chỉnh nếu truyện có AllowCustomization = true
            if (chapter.Story.AllowCustomization)
            {
                var user = await _userManager.GetUserAsync(User);
                string userId = user?.Id ?? "anonymous";

                if (user != null) // Người dùng đã đăng nhập
                {
                    var customization = await _context.ReaderStoryCustomizations
                        .FirstOrDefaultAsync(rsc => rsc.StoryId == chapter.StoryId && rsc.UserId == userId);

                    if (customization == null)
                    {
                        return RedirectToAction("Customize", "Story", new { storyId = chapter.StoryId, returnUrl = Url.Action("InteractiveRead", "Segment", new { id = chapter.Segments.OrderBy(s => s.Id).FirstOrDefault()?.Id }) });
                    }
                }
                else // Người dùng chưa đăng nhập
                {
                    var sessionKey = $"Customization_{chapter.StoryId}";
                    var sessionData = HttpContext.Session.GetString(sessionKey);
                    if (string.IsNullOrEmpty(sessionData))
                    {
                        return RedirectToAction("Customize", "Story", new { storyId = chapter.StoryId, returnUrl = Url.Action("InteractiveRead", "Segment", new { id = chapter.Segments.OrderBy(s => s.Id).FirstOrDefault()?.Id }) });
                    }
                }
            }

            var firstSegment = await _context.ChapterSegments
                .Where(s => s.ChapterId == chapter.Id)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (firstSegment == null)
                return RedirectToAction("Details", "Story", new { id = chapter.StoryId, errorMessage = "Chương chưa có đoạn nào." });

            return RedirectToAction("InteractiveRead", "Segment", new { id = firstSegment.Id });
        }

        [Authorize]
        public async Task<IActionResult> Manage(int storyId)
        {
            var story = await _context.Stories.FindAsync(storyId);
            if (story == null)
            {
                TempData["ErrorMessage"] = "Truyện không tồn tại.";
                return RedirectToAction("MyProfile", "Account");
            }

            var user = await _userManager.GetUserAsync(User);
            if (story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền quản lý truyện này.";
                return RedirectToAction("MyProfile", "Account");
            }

            var chapters = await _context.Chapters
                .Where(c => c.StoryId == storyId)
                .Include(c => c.Segments)
                    .ThenInclude(s => s.Choices)
                        .ThenInclude(c => c.NextSegment)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            ViewBag.StoryId = storyId;
            return View(chapters);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var chapter = await _context.Chapters
                .Include(c => c.Story)
                .Include(c => c.Segments)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (chapter == null)
            {
                TempData["ErrorMessage"] = "Chương không tồn tại.";
                return RedirectToAction("Manage", new { storyId = ViewBag.StoryId });
            }

            var user = await _userManager.GetUserAsync(User);
            if (chapter.Story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xóa chương này.";
                return RedirectToAction("Manage", new { storyId = chapter.StoryId });
            }

            // Tìm tất cả các ChapterSegment thuộc Chapter
            var segmentIds = chapter.Segments.Select(s => s.Id).ToList();

            // Tìm tất cả các Choices có NextSegmentId tham chiếu đến các ChapterSegment của Chapter
            var choicesReferencingSegments = await _context.Choices
                .Where(c => segmentIds.Contains(c.NextSegmentId))
                .ToListAsync();

            // Xóa các Choices này để tránh xung đột ràng buộc
            _context.Choices.RemoveRange(choicesReferencingSegments);

            // Đặt ChapterSegmentId trong ReadingProgress thành null trước khi xóa
            var relatedProgresses = await _context.ReadingProgresses
                .Where(rp => segmentIds.Contains(rp.ChapterSegmentId.Value))
                .ToListAsync();

            foreach (var progress in relatedProgresses)
            {
                progress.ChapterSegmentId = null;
            }

            _context.Chapters.Remove(chapter);
            await _context.SaveChangesAsync();

            // Sau khi xóa chương, kiểm tra xem Story có còn chương công khai nào có đoạn không
            var story = await _context.Stories
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.Segments)
                .FirstOrDefaultAsync(s => s.Id == chapter.StoryId);

            if (story != null)
            {
                var hasValidPublicChapter = story.Chapters
                    .Any(c => c.IsPublic && c.Segments != null && c.Segments.Any());

                if (!hasValidPublicChapter && story.IsPublic)
                {
                    // Nếu không còn chương công khai nào có đoạn, đặt Story thành không công khai
                    story.IsPublic = false;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["SuccessMessage"] = "Chương đã được xóa thành công!";
            return RedirectToAction("Manage", new { storyId = chapter.StoryId });
        }
    }
}
