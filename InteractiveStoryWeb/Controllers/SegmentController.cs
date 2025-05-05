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
        [Authorize]
        public async Task<IActionResult> Create(ChapterSegmentCreateViewModel model)
        {
            var chapter = await _context.Chapters
                .Include(c => c.Story)
                .FirstOrDefaultAsync(c => c.Id == model.ChapterId);

            if (chapter == null)
            {
                TempData["ErrorMessage"] = "Chương không tồn tại.";
                return RedirectToAction("Manage", "Chapter", new { storyId = ViewBag.StoryId });
            }

            var user = await _userManager.GetUserAsync(User);
            if (chapter.Story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thêm đoạn vào chương này.";
                return RedirectToAction("Manage", "Chapter", new { storyId = chapter.StoryId });
            }

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

            var segment = new ChapterSegment
            {
                ChapterId = model.ChapterId,
                Title = model.Title,
                Content = model.Content,
                ImageUrl = imagePath,
                ImagePosition = model.ImagePosition, // Lưu giá trị ImagePosition
                CreatedAt = DateTime.Now
            };

            _context.ChapterSegments.Add(segment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đoạn đã được tạo thành công!";
            return RedirectToAction("Manage", "Chapter", new { storyId = chapter.StoryId });
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

            // Thay thế từ khóa và định dạng Markdown
            ReaderStoryCustomization customization = null;
            if (story.AllowCustomization)
            {
                string userId = user?.Id ?? "anonymous";

                if (user != null)
                {
                    customization = await _context.ReaderStoryCustomizations
                        .FirstOrDefaultAsync(rsc => rsc.StoryId == story.Id && rsc.UserId == userId);
                }
                else
                {
                    var sessionKey = $"Customization_{story.Id}";
                    var sessionData = HttpContext.Session.GetString(sessionKey);
                    if (!string.IsNullOrEmpty(sessionData))
                    {
                        customization = JsonSerializer.Deserialize<ReaderStoryCustomization>(sessionData);
                    }
                }
            }

            // Chuyển đổi nội dung thành HTML với Markdown và tùy chỉnh
            segment.Content = MarkdownFormatter.FormatContent(segment.Content, customization);

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

        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var segment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                .ThenInclude(c => c.Story)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (segment == null)
                return NotFound("Đoạn không tồn tại.");

            var user = await _userManager.GetUserAsync(User);
            if (segment.Chapter.Story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa đoạn này.";
                return RedirectToAction("Manage", "Chapter", new { storyId = segment.Chapter.StoryId });
            }

            var model = new ChapterSegmentEditViewModel
            {
                Id = segment.Id,
                ChapterId = segment.ChapterId,
                Title = segment.Title,
                Content = segment.Content,
                ImageUrl = segment.ImageUrl,
                ImagePosition = segment.ImagePosition,
                CreatedAt = segment.CreatedAt
            };

            ViewBag.StoryId = segment.Chapter.StoryId;
            ViewBag.AllowCustomization = segment.Chapter.Story.AllowCustomization;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(ChapterSegmentEditViewModel model)
        {
            var segment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                .ThenInclude(c => c.Story)
                .FirstOrDefaultAsync(s => s.Id == model.Id);

            if (segment == null)
                return NotFound("Đoạn không tồn tại.");

            var user = await _userManager.GetUserAsync(User);
            if (segment.Chapter.Story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa đoạn này.";
                return RedirectToAction("Manage", "Chapter", new { storyId = segment.Chapter.StoryId });
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin.";
                return View(model);
            }

            // Giữ giá trị ImageUrl hiện tại nếu không có ảnh mới
            string imagePath = model.ImageUrl;
            if (model.NewImage != null && model.NewImage.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads/segments");
                Directory.CreateDirectory(uploads);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.NewImage.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.NewImage.CopyToAsync(stream);
                }

                imagePath = "/uploads/segments/" + fileName;

                if (!string.IsNullOrEmpty(segment.ImageUrl))
                {
                    var oldImagePath = Path.Combine(_env.WebRootPath, segment.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }
            }

            segment.Title = model.Title;
            segment.Content = model.Content;
            segment.ImageUrl = imagePath;
            segment.ImagePosition = model.ImagePosition;
            segment.CreatedAt = model.CreatedAt;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đoạn đã được cập nhật thành công!";
            return RedirectToAction("Manage", "Chapter", new { storyId = segment.Chapter.StoryId });
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
                return Json(new { success = false, message = "Đoạn không tồn tại." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (segment.Chapter.Story.AuthorId != user.Id)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa đoạn này." });
            }

            // Kiểm tra xem đoạn có được liên kết bởi các lựa chọn khác không (NextSegmentId)
            var linkedChoices = await _context.Choices
                .Where(c => c.NextSegmentId == segment.Id)
                .ToListAsync();

            if (linkedChoices.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Không thể xóa đoạn này vì nó được liên kết bởi các lựa chọn khác.",
                    storyId = segment.Chapter.StoryId
                });
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

            // Sau khi xóa đoạn, kiểm tra xem chương có còn đoạn nào không
            var chapter = await _context.Chapters
                .Include(c => c.Segments)
                .Include(c => c.Story)
                .FirstOrDefaultAsync(c => c.Id == segment.ChapterId);

            if (chapter != null && chapter.Segments != null && !chapter.Segments.Any())
            {
                // Nếu chương không còn đoạn nào, set chương thành không công khai
                chapter.IsPublic = false;

                // Kiểm tra xem Story có còn chương công khai nào có đoạn không
                var story = chapter.Story;
                var hasValidPublicChapter = await _context.Chapters
                    .Include(c => c.Segments)
                    .AnyAsync(c => c.StoryId == story.Id && c.IsPublic && c.Segments.Any());

                if (!hasValidPublicChapter && story.IsPublic)
                {
                    // Nếu không còn chương công khai nào có đoạn, đặt Story thành không công khai
                    story.IsPublic = false;
                }

                await _context.SaveChangesAsync();
            }

            return Json(new
            {
                success = true,
                message = "Đoạn đã được xóa thành công!",
                chapterId = segment.ChapterId
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Preview(int chapterId, string content, ImagePosition imagePosition, IFormFile image, string imageUrl)
        {
            var chapter = await _context.Chapters
                .Include(c => c.Story)
                .FirstOrDefaultAsync(c => c.Id == chapterId);

            if (chapter == null)
            {
                return Json(new { success = false, message = "Chương không tồn tại." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (chapter.Story.AuthorId != user.Id)
            {
                return Json(new { success = false, message = "Bạn không có quyền xem trước đoạn này." });
            }

            ReaderStoryCustomization customization = null;
            if (chapter.Story.AllowCustomization)
            {
                string userId = user.Id;
                customization = await _context.ReaderStoryCustomizations
                    .FirstOrDefaultAsync(rsc => rsc.StoryId == chapter.StoryId && rsc.UserId == userId);

                if (customization == null)
                {
                    customization = new ReaderStoryCustomization
                    {
                        Name = "Người đọc",
                        FirstPersonPronoun = "Tôi",
                        SecondPersonPronoun = "Bạn"
                    };
                }
            }

            var previewContent = MarkdownFormatter.FormatContent(content, customization);
            var html = new System.Text.StringBuilder();

            string finalImageUrl = imageUrl; // Sử dụng imageUrl nếu không có ảnh mới
            if (image != null && image.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads/temp");
                Directory.CreateDirectory(uploads);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                finalImageUrl = "/uploads/temp/" + fileName;
            }

            if (!string.IsNullOrEmpty(finalImageUrl) && imagePosition == ImagePosition.Top)
            {
                html.AppendLine("<div class=\"text-center mb-3\">");
                html.AppendLine($"<img src=\"{finalImageUrl}\" class=\"img-fluid rounded shadow segment-image\" alt=\"Ảnh minh họa\" />");
                html.AppendLine("</div>");
            }

            html.AppendLine(previewContent);

            if (!string.IsNullOrEmpty(finalImageUrl) && imagePosition == ImagePosition.Bottom)
            {
                html.AppendLine("<div class=\"text-center mb-3\">");
                html.AppendLine($"<img src=\"{finalImageUrl}\" class=\"img-fluid rounded shadow segment-image\" alt=\"Ảnh minh họa\" />");
                html.AppendLine("</div>");
            }

            return Json(new { success = true, previewContent = html.ToString() });
        }
    }
}
