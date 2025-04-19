using System.Text.RegularExpressions;
using InteractiveStoryWeb.Data;
using InteractiveStoryWeb.Models;
using InteractiveStoryWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using InteractiveStoryWeb.Utils;

namespace InteractiveStoryWeb.Controllers
{
    [Authorize]
    public class SegmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;

        public SegmentController(ApplicationDbContext context, IWebHostEnvironment env, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Create(int chapterId)
        {
            var chapter = _context.Chapters.Find(chapterId);
            if (chapter == null) return NotFound();
            ViewBag.StoryId = chapter.StoryId;
            ViewBag.AllowCustomization = chapter.Story?.AllowCustomization ?? false;
            return View(new ChapterSegmentCreateViewModel { ChapterId = chapterId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ChapterSegmentCreateViewModel model)
        {
            // Kiểm tra ChapterId có tồn tại không
            var chapter = await _context.Chapters
                .Include(c => c.Story) // Bao gồm Story để lấy StoryId
                .FirstOrDefaultAsync(c => c.Id == model.ChapterId);

            if (chapter == null)
            {
                TempData["ErrorMessage"] = "Chương không tồn tại.";
                return RedirectToAction("Index", "Story"); // Chuyển hướng về danh sách truyện nếu chương không tồn tại
            }

            if (!ModelState.IsValid)
            {
                ViewBag.StoryId = chapter.StoryId;
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin.";
                return View(model);
            }

            string? imagePath = null;
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

            var segment = new ChapterSegment
            {
                ChapterId = model.ChapterId,
                Title = model.Title,
                Content = model.Content,
                ImageUrl = imagePath,
                CreatedAt = DateTime.Now
            };

            _context.ChapterSegments.Add(segment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đoạn đã được tạo thành công!";
            return RedirectToAction("Manage", "Chapter", new { storyId = chapter.StoryId }); // Sử dụng chapter.StoryId đã được tải
        }

        [AllowAnonymous]
        public async Task<IActionResult> InteractiveRead(int id)
        {
            var segment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                    .ThenInclude(c => c.Story)
                .Include(s => s.Choices)
                    .ThenInclude(c => c.NextSegment)
                        .ThenInclude(ns => ns.Chapter)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (segment == null)
                return NotFound("Đoạn không tồn tại.");

            var chapter = segment.Chapter;
            var story = chapter.Story;

            if (chapter == null || !chapter.IsPublic || story == null || !story.IsPublic)
                return NotFound("Nội dung chưa được công khai.");

            // Tăng lượt xem chapter chỉ khi vào đoạn đầu tiên
            var firstSegment = await _context.ChapterSegments
                .Where(s => s.ChapterId == chapter.Id && s.Chapter.IsPublic)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();
            if (segment.Id == firstSegment?.Id)
            {
                chapter.ViewCount++;
                await _context.SaveChangesAsync();
            }

            // Lưu tiến trình đọc nếu người dùng đã đăng nhập
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var progress = await _context.ReadingProgresses
                    .FirstOrDefaultAsync(rp => rp.UserId == user.Id && rp.StoryId == story.Id);

                if (progress == null)
                {
                    // Tạo mới tiến trình
                    progress = new ReadingProgress
                    {
                        UserId = user.Id,
                        StoryId = story.Id,
                        ChapterSegmentId = segment.Id,
                        LastReadAt = DateTime.Now
                    };
                    _context.ReadingProgresses.Add(progress);
                }
                else
                {
                    progress.ChapterSegmentId = segment.Id;
                    progress.LastReadAt = DateTime.Now;
                }
                await _context.SaveChangesAsync();
            }

            // Thay thế từ khóa nếu tính năng cá nhân hóa được bật
            ReaderStoryCustomization customization = null;
            if (story.AllowCustomization)
            {
                string userId = user?.Id ?? "anonymous";

                if (user != null) // Người dùng đã đăng nhập, đọc từ database
                {
                    customization = await _context.ReaderStoryCustomizations
                        .FirstOrDefaultAsync(rsc => rsc.StoryId == story.Id && rsc.UserId == userId);
                }
                else // Người dùng chưa đăng nhập, đọc từ session
                {
                    var sessionKey = $"Customization_{story.Id}";
                    var sessionData = HttpContext.Session.GetString(sessionKey);
                    if (!string.IsNullOrEmpty(sessionData))
                    {
                        customization = JsonSerializer.Deserialize<ReaderStoryCustomization>(sessionData);
                    }
                }

                if (customization != null)
                {
                    // Debug dữ liệu customization
                    if (string.IsNullOrEmpty(customization.Name))
                    {
                        Console.WriteLine($"Customization Name is empty for UserId: {userId}, StoryId: {story.Id}");
                    }
                    if (string.IsNullOrEmpty(customization.FirstPersonPronoun))
                    {
                        Console.WriteLine($"Customization FirstPersonPronoun is empty for UserId: {userId}, StoryId: {story.Id}");
                    }
                    if (string.IsNullOrEmpty(customization.SecondPersonPronoun))
                    {
                        Console.WriteLine($"Customization SecondPersonPronoun is empty for UserId: {userId}, StoryId: {story.Id}");
                    }

                    // Thay thế từ khóa với viết hoa theo ngữ cảnh
                    segment.Content = TextFormatter.ReplaceWithContextualCapitalization(segment.Content, "[Tên]", customization.Name);
                    segment.Content = TextFormatter.ReplaceWithContextualCapitalization(segment.Content, "[XưngHôThứNhất]", customization.FirstPersonPronoun);
                    segment.Content = TextFormatter.ReplaceWithContextualCapitalization(segment.Content, "[XưngHôThứHai]", customization.SecondPersonPronoun);
                }
                else
                {
                    Console.WriteLine($"Customization is null for UserId: {userId}, StoryId: {story.Id}");
                }
            }

            // Lọc các lựa chọn
            segment.Choices = segment.Choices
                .Where(c => c.NextSegment != null &&
                            c.NextSegment.ChapterId == segment.ChapterId &&
                            c.NextSegment.Chapter.StoryId == story.Id &&
                            c.NextSegment.Chapter.IsPublic)
                .ToList();

            ViewBag.StoryId = story.Id;
            ViewBag.CurrentChapterId = chapter.Id;
            ViewBag.Customization = customization;

            var hasNextChapter = await _context.Chapters
                .AnyAsync(c => c.StoryId == story.Id && c.IsPublic && c.CreatedAt > chapter.CreatedAt);

            ViewBag.HasNextChapter = hasNextChapter;

            return View(segment);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> NextChapter(int currentSegmentId)
        {
            var currentSegment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                    .ThenInclude(c => c.Story)
                .FirstOrDefaultAsync(s => s.Id == currentSegmentId);

            if (currentSegment == null || currentSegment.Chapter == null || currentSegment.Chapter.Story == null)
                return NotFound();

            var storyId = currentSegment.Chapter.StoryId;
            var currentChapter = currentSegment.Chapter;

            var nextChapter = await _context.Chapters
                .Where(c => c.StoryId == storyId && c.IsPublic && c.CreatedAt > currentChapter.CreatedAt)
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (nextChapter != null)
            {
                var firstSegment = await _context.ChapterSegments
                    .Where(s => s.ChapterId == nextChapter.Id)
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync();

                if (firstSegment != null)
                {
                    return RedirectToAction("InteractiveRead", new { id = firstSegment.Id });
                }
            }

            TempData["Message"] = "Không có chương tiếp theo.";
            return RedirectToAction("InteractiveRead", new { id = currentSegmentId });
        }



        [HttpGet]
        public async Task<IActionResult> GetSegmentJson(int id)
        {
            var segment = await _context.ChapterSegments
                .Include(s => s.Choices)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (segment == null)
                return NotFound();

            return Json(new
            {
                id = segment.Id,
                content = segment.Content,
                imageUrl = segment.ImageUrl,
                choices = segment.Choices.Select(c => new
                {
                    text = c.ChoiceText,
                    nextId = c.NextSegmentId
                })
            });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var segment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (segment == null) return NotFound();

            ViewBag.StoryId = segment.Chapter.StoryId; // Gán StoryId cho nút "Quay lại"
            ViewBag.AllowCustomization = segment.Chapter.Story?.AllowCustomization ?? false;
            return View(segment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ChapterSegment model, IFormFile? NewImage)
        {
            // Xóa lỗi cho các trường không cần thiết
            ModelState.Remove("CreatedAt");
            ModelState.Remove("Chapter");
            ModelState.Remove("Choices");

            if (!ModelState.IsValid)
            {
                var segmentTemp = await _context.ChapterSegments
                    .Include(s => s.Chapter)
                    .FirstOrDefaultAsync(s => s.Id == model.Id);
                ViewBag.StoryId = segmentTemp?.Chapter?.StoryId;
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin.";
                return View(model);
            }

            var segment = await _context.ChapterSegments.FindAsync(model.Id);
            if (segment == null) return NotFound();

            segment.Title = model.Title;
            segment.Content = model.Content;
            segment.CreatedAt = segment.CreatedAt;
            segment.UpdatedAt = DateTime.Now;

            if (NewImage != null && NewImage.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads/segments");
                Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(NewImage.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await NewImage.CopyToAsync(stream);
                }

                segment.ImageUrl = "/uploads/segments/" + fileName;
            }

            await _context.SaveChangesAsync();

            var chapter = await _context.Chapters
                .FirstOrDefaultAsync(c => c.Id == segment.ChapterId);

            if (chapter == null) return NotFound();

            TempData["SuccessMessage"] = "Đoạn đã được cập nhật thành công!";
            return RedirectToAction("Manage", "Chapter", new { storyId = chapter.StoryId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var segment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                    .ThenInclude(c => c.Story)
                .Include(s => s.Choices)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (segment == null)
            {
                TempData["ErrorMessage"] = "Đoạn không tồn tại.";
                return RedirectToAction("Manage", "Chapter", new { storyId = ViewBag.StoryId });
            }

            var user = await _userManager.GetUserAsync(User);
            if (segment.Chapter.Story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xóa đoạn này.";
                return RedirectToAction("Manage", "Chapter", new { storyId = segment.Chapter.StoryId });
            }

            // Kiểm tra xem đoạn có được liên kết bởi các lựa chọn khác không (NextSegmentId)
            var linkedChoices = await _context.Choices
                .Where(c => c.NextSegmentId == segment.Id)
                .ToListAsync();

            if (linkedChoices.Any())
            {
                TempData["ErrorMessage"] = "Không thể xóa đoạn này vì nó được liên kết bởi các lựa chọn khác.";
                return RedirectToAction("Manage", "Chapter", new { storyId = segment.Chapter.StoryId });
            }

            // Đặt ChapterSegmentId trong ReadingProgress thành null trước khi xóa
            var relatedProgresses = await _context.ReadingProgresses
                .Where(rp => rp.ChapterSegmentId == segment.Id)
                .ToListAsync();

            foreach (var progress in relatedProgresses)
            {
                progress.ChapterSegmentId = null;
            }

            _context.ChapterSegments.Remove(segment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đoạn đã được xóa thành công!";
            return RedirectToAction("Manage", "Chapter", new { storyId = segment.Chapter.StoryId });
        }
    }
}
