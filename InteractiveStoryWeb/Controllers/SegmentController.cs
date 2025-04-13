using InteractiveStoryWeb.Data;
using InteractiveStoryWeb.Models;
using InteractiveStoryWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InteractiveStoryWeb.Controllers
{
    [Authorize]
    public class SegmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SegmentController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet]
        public IActionResult Create(int chapterId)
        {
            var chapter = _context.Chapters.Find(chapterId);
            if (chapter == null) return NotFound();
            ViewBag.StoryId = chapter.StoryId; // Gán StoryId cho nút "Quay lại"
            return View(new ChapterSegmentCreateViewModel { ChapterId = chapterId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ChapterSegmentCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var chapter = await _context.Chapters.FindAsync(model.ChapterId);
                ViewBag.StoryId = chapter?.StoryId;
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

            var secondSegment = new ChapterSegment
            {
                ChapterId = model.ChapterId,
                Title = "Đoạn 2",
                Content = "Nội dung đoạn 2",
                CreatedAt = DateTime.Now
            };
            _context.ChapterSegments.Add(secondSegment);
            await _context.SaveChangesAsync();

            var choice = new Choice
            {
                ChapterSegmentId = segment.Id,
                ChoiceText = "Sang đoạn 2",
                NextSegmentId = secondSegment.Id,
                CreatedAt = DateTime.Now
            };
            _context.Choices.Add(choice);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đoạn đã được tạo thành công!";
            return RedirectToAction("Create", "Segment", new { chapterId = model.ChapterId });
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
                chapter.ViewCount++; // Tăng ViewCount của Chapter
                await _context.SaveChangesAsync();
            }

            // Lọc các lựa chọn: chỉ giữ lại nếu NextSegment tồn tại, cùng chương, cùng truyện, và chương public
            segment.Choices = segment.Choices
                .Where(c => c.NextSegment != null &&
                            c.NextSegment.ChapterId == segment.ChapterId &&
                            c.NextSegment.Chapter.StoryId == story.Id &&
                            c.NextSegment.Chapter.IsPublic)
                .ToList();

            // Gán ViewBag để hiển thị nút chuyển chương
            ViewBag.StoryId = story.Id;
            ViewBag.CurrentChapterId = chapter.Id;

            // Kiểm tra xem có chương tiếp theo không
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

            TempData["SuccessMessage"] = "Đoạn đã được cập nhật thành công!";
            return RedirectToAction("Manage", "Chapter", new { storyId = segment.ChapterId });
        }
    }
}
