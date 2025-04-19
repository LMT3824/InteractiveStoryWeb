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

        [Authorize]
        public IActionResult Create(int storyId)
        {
            return View(new ChapterCreateViewModel { StoryId = storyId });
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
                IsPublic = model.IsPublic,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.Chapters.Add(chapter);
            await _context.SaveChangesAsync();

            var segment = new ChapterSegment
            {
                ChapterId = chapter.Id,
                Title = model.FirstSegmentTitle,
                Content = model.FirstSegmentContent,
                ImageUrl = imagePath,
                CreatedAt = DateTime.Now
            };
            _context.ChapterSegments.Add(segment);
            await _context.SaveChangesAsync();

            // Tạo đoạn thứ hai và lựa chọn (đã sửa trước đó)
            var secondSegment = new ChapterSegment
            {
                ChapterId = chapter.Id,
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

            TempData["SuccessMessage"] = "Chương đã được tạo thành công!";
            return RedirectToAction("Manage", "Chapter", new { storyId = chapter.StoryId });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var chapter = await _context.Chapters.FindAsync(id);
            if (chapter == null) return NotFound();

            return View(chapter);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Chapter model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin.";
                return View(model);
            }

            var chapter = await _context.Chapters.FindAsync(model.Id);
            if (chapter == null) return NotFound();

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

            chapter.ViewCount++;
            await _context.SaveChangesAsync();

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
            var chapters = await _context.Chapters
                .Where(c => c.StoryId == storyId)
                .Include(c => c.Segments)
                    .ThenInclude(s => s.Choices) // ✅ Thêm dòng này
                        .ThenInclude(c => c.NextSegment) // ✅ Nếu muốn hiện tiêu đề đoạn tiếp theo
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

            // Kiểm tra xem đây có phải là chương duy nhất của truyện không
            var chapterCount = await _context.Chapters
                .Where(c => c.StoryId == chapter.StoryId)
                .CountAsync();

            if (chapterCount <= 1)
            {
                TempData["ErrorMessage"] = "Đây là chương duy nhất của truyện. Bạn chỉ có thể xóa toàn bộ truyện.";
                return RedirectToAction("Manage", new { storyId = chapter.StoryId });
            }

            // Đặt ChapterSegmentId trong ReadingProgress thành null trước khi xóa
            var segmentIds = chapter.Segments.Select(s => s.Id).ToList();
            var relatedProgresses = await _context.ReadingProgresses
                .Where(rp => segmentIds.Contains(rp.ChapterSegmentId.Value))
                .ToListAsync();

            foreach (var progress in relatedProgresses)
            {
                progress.ChapterSegmentId = null;
            }

            _context.Chapters.Remove(chapter);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Chương đã được xóa thành công!";
            return RedirectToAction("Manage", new { storyId = chapter.StoryId });
        }
    }
}
