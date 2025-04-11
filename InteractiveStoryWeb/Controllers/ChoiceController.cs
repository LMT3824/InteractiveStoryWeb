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

        // GET: Choice/Create?chapterId=1
        public IActionResult Create(int chapterId)
        {
            var vm = new ChoiceCreateViewModel { ChapterId = chapterId };
            return View(vm);
        }

        // POST: Choice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ChoiceCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Tạo chương kế tiếp để chuyển tới ngay sau khi thêm lựa chọn
            var nextChapter = new Chapter
            {
                StoryId = _context.Chapters.First(c => c.Id == model.ChapterId).StoryId,
                ParentChapterId = model.ChapterId,
                Content = "[Nội dung chương mới]", // Có thể để rỗng hoặc gợi ý người dùng cập nhật sau
                CreatedAt = DateTime.Now
            };
            _context.Chapters.Add(nextChapter);
            await _context.SaveChangesAsync();

            var choice = new Choice
            {
                ChapterId = model.ChapterId,
                ChoiceText = model.ChoiceText,
                NextChapterId = nextChapter.Id
            };

            _context.Choices.Add(choice);
            await _context.SaveChangesAsync();

            return RedirectToAction("Edit", "Chapter", new { id = nextChapter.Id });
        }

        // GET: Choice/Manage?chapterId=xxx
        public async Task<IActionResult> Manage(int chapterId)
        {
            var chapter = await _context.Chapters
                .Include(c => c.Choices)
                .FirstOrDefaultAsync(c => c.Id == chapterId);

            if (chapter == null)
                return NotFound();

            return View(chapter); // truyền model rõ ràng
        }

        // POST: Choice/Add
        [HttpPost]
        public async Task<IActionResult> Add(Choice model)
        {
            if (model.NextChapterId == 0 || !_context.Chapters.Any(c => c.Id == model.NextChapterId))
            {
                TempData["Error"] = "Vui lòng nhập đúng ID chương tiếp theo.";
                return RedirectToAction("Manage", new { chapterId = model.ChapterId });
            }

            var choice = new Choice
            {
                ChapterId = model.ChapterId,
                ChoiceText = model.ChoiceText,
                NextChapterId = model.NextChapterId
            };

            _context.Choices.Add(choice);
            await _context.SaveChangesAsync();

            return RedirectToAction("Manage", new { chapterId = model.ChapterId });
        }

        // POST: Choice/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Choice updatedChoice)
        {
            if (id != updatedChoice.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(updatedChoice);
            }

            try
            {
                _context.Update(updatedChoice);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Choices.Any(c => c.Id == updatedChoice.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToAction("Manage", new { chapterId = updatedChoice.ChapterId });
        }
    }
}
