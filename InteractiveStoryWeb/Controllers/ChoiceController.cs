using InteractiveStoryWeb.Data;
using InteractiveStoryWeb.Models;
using InteractiveStoryWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InteractiveStoryWeb.Controllers
{
    [Authorize]
    public class ChoiceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ChoiceController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(ChoiceCreateViewModel model)
        {
            var segment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                .FirstOrDefaultAsync(s => s.Id == model.ChapterSegmentId);

            if (segment == null)
            {
                TempData["ErrorMessage"] = "Đoạn không tồn tại.";
                return RedirectToAction("Index", "Story");
            }

            if (!ModelState.IsValid)
            {
                var availableSegments = await _context.ChapterSegments
                    .Where(s => s.ChapterId == segment.ChapterId)
                    .ToListAsync();

                ViewBag.Segment = segment;
                ViewBag.AvailableSegments = availableSegments;
                ViewBag.StoryId = segment.Chapter.StoryId;
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin.";
                return View("Manage", model);
            }

            // Kiểm tra đoạn hiện tại
            var currentSegment = await _context.ChapterSegments.FindAsync(model.ChapterSegmentId);
            if (currentSegment == null)
            {
                var availableSegments = await _context.ChapterSegments
                    .Where(s => s.ChapterId == segment.ChapterId)
                    .ToListAsync();

                ViewBag.Segment = segment;
                ViewBag.AvailableSegments = availableSegments;
                ViewBag.StoryId = segment.Chapter.StoryId;
                ModelState.AddModelError("ChapterSegmentId", "Đoạn hiện tại không tồn tại.");
                TempData["ErrorMessage"] = "Đoạn hiện tại không tồn tại.";
                return View("Manage", model);
            }

            // Kiểm tra NextSegmentId
            var nextSegment = await _context.ChapterSegments
                .FirstOrDefaultAsync(s => s.Id == model.NextSegmentId && s.ChapterId == currentSegment.ChapterId);
            if (nextSegment == null)
            {
                var availableSegments = await _context.ChapterSegments
                    .Where(s => s.ChapterId == segment.ChapterId)
                    .ToListAsync();

                ViewBag.Segment = segment;
                ViewBag.AvailableSegments = availableSegments;
                ViewBag.StoryId = segment.Chapter.StoryId;
                ModelState.AddModelError("NextSegmentId", "Đoạn tiếp theo không tồn tại hoặc không thuộc cùng chương.");
                TempData["ErrorMessage"] = "Đoạn tiếp theo không hợp lệ.";
                return View("Manage", model);
            }

            var choice = new Choice
            {
                ChapterSegmentId = model.ChapterSegmentId,
                ChoiceText = model.ChoiceText,
                NextSegmentId = model.NextSegmentId,
                CreatedAt = DateTime.Now
            };

            _context.Choices.Add(choice);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Lựa chọn đã được thêm thành công!";
            return RedirectToAction("Manage", "Chapter", new { storyId = segment.Chapter.StoryId });
        }

        // Thêm action Delete
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var choice = await _context.Choices
                .Include(c => c.ChapterSegment)
                    .ThenInclude(cs => cs.Chapter)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (choice == null)
            {
                TempData["ErrorMessage"] = "Lựa chọn không tồn tại.";
                return RedirectToAction("Index", "Story");
            }

            var storyId = choice.ChapterSegment.Chapter.StoryId;
            _context.Choices.Remove(choice);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Lựa chọn đã được xóa thành công!";
            return RedirectToAction("Manage", "Chapter", new { storyId = storyId });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var choice = await _context.Choices
                .Include(c => c.ChapterSegment)
                    .ThenInclude(cs => cs.Chapter)
                .Include(c => c.NextSegment)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (choice == null)
            {
                return Json(new { success = false, message = "Lựa chọn không tồn tại." });
            }

            return Json(new
            {
                success = true,
                id = choice.Id,
                chapterSegmentId = choice.ChapterSegmentId,
                createdAt = choice.CreatedAt,
                choiceText = choice.ChoiceText,
                nextSegmentId = choice.NextSegmentId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Choice model)
        {
            ModelState.Remove("CreatedAt");
            ModelState.Remove("ChapterSegment");
            ModelState.Remove("NextSegment");

            var choice = await _context.Choices
                .Include(c => c.ChapterSegment)
                    .ThenInclude(cs => cs.Chapter)
                .FirstOrDefaultAsync(c => c.Id == model.Id);

            if (choice == null)
            {
                return Json(new { success = false, message = "Lựa chọn không tồn tại." });
            }

            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Vui lòng kiểm tra lại thông tin." });
            }

            var currentSegment = await _context.ChapterSegments.FindAsync(model.ChapterSegmentId);
            if (currentSegment == null)
            {
                return Json(new { success = false, message = "Đoạn hiện tại không tồn tại." });
            }

            var nextSegment = await _context.ChapterSegments
                .FirstOrDefaultAsync(s => s.Id == model.NextSegmentId && s.ChapterId == currentSegment.ChapterId);
            if (nextSegment == null)
            {
                return Json(new { success = false, message = "Đoạn tiếp theo không tồn tại hoặc không thuộc cùng chương." });
            }

            choice.ChoiceText = model.ChoiceText;
            choice.NextSegmentId = model.NextSegmentId;
            choice.CreatedAt = choice.CreatedAt;
            choice.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Lựa chọn đã được cập nhật thành công!",
                choiceText = choice.ChoiceText,
                nextSegmentTitle = nextSegment.Title
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create(ChoiceCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Kiểm tra đoạn hiện tại và đoạn tiếp theo phải cùng chương
            var currentSegment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                .FirstOrDefaultAsync(s => s.Id == model.ChapterSegmentId);

            var nextSegment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                .FirstOrDefaultAsync(s => s.Id == model.NextSegmentId);

            if (currentSegment == null || nextSegment == null || currentSegment.ChapterId != nextSegment.ChapterId)
            {
                ModelState.AddModelError("", "Lựa chọn phải dẫn tới đoạn trong cùng chương.");
                return View(model);
            }

            var choice = new Choice
            {
                ChapterSegmentId = model.ChapterSegmentId,
                ChoiceText = model.ChoiceText,
                NextSegmentId = model.NextSegmentId,
                CreatedAt = DateTime.Now
            };

            _context.Choices.Add(choice);
            await _context.SaveChangesAsync();

            return RedirectToAction("Manage", new { chapterSegmentId = model.ChapterSegmentId });
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Choose(int id)
        {
            var choice = await _context.Choices
                .Include(c => c.ChapterSegment)
                    .ThenInclude(s => s.Chapter)
                        .ThenInclude(c => c.Story)
                .Include(c => c.NextSegment)
                    .ThenInclude(ns => ns.Chapter)
                        .ThenInclude(c => c.Story)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (choice == null || choice.ChapterSegment == null ||
                choice.ChapterSegment.Chapter == null ||
                choice.ChapterSegment.Chapter.Story == null)
                return NotFound("Lựa chọn không tồn tại.");

            // Kiểm tra truyện/chương có được public không
            if (!choice.ChapterSegment.Chapter.IsPublic ||
                !choice.ChapterSegment.Chapter.Story.IsPublic)
                return NotFound("Truyện hoặc chương không công khai.");

            if (choice.NextSegmentId == null || choice.NextSegment == null)
                return Content("Lựa chọn chưa có nội dung tiếp theo.");

            // Kiểm tra NextSegment có cùng truyện và chương public
            if (choice.NextSegment.Chapter.StoryId != choice.ChapterSegment.Chapter.StoryId ||
                !choice.NextSegment.Chapter.IsPublic)
                return NotFound("Lỗi lựa chọn: không khớp truyện hoặc chương không công khai.");

            return RedirectToAction("InteractiveRead", "Segment", new { id = choice.NextSegmentId });
        }

    }
}
