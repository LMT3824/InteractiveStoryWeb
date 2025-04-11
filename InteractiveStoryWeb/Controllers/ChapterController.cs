using InteractiveStoryWeb.Data;
using InteractiveStoryWeb.Models;
using InteractiveStoryWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InteractiveStoryWeb.Controllers
{
    [Authorize]
    public class ChapterController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ChapterController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: Chapter/Create?storyId=1
        public IActionResult Create(int storyId, int? parentChapterId)
        {
            var vm = new ChapterCreateViewModel
            {
                StoryId = storyId,
                ParentChapterId = parentChapterId
            };
            return View(vm);
        }

        // POST: Chapter/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ChapterCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string imagePath = null;

            if (model.Image != null && model.Image.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads/chapters");
                Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.Image.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Image.CopyToAsync(stream);
                }

                imagePath = "/uploads/chapters/" + fileName;
            }

            var chapter = new Chapter
            {
                StoryId = model.StoryId,
                ParentChapterId = model.ParentChapterId,
                Content = model.Content,
                ImageUrl = imagePath, // null nếu không có ảnh
                CreatedAt = DateTime.Now
            };

            _context.Chapters.Add(chapter);
            await _context.SaveChangesAsync();

            // TODO: chuyển sang quản lý lựa chọn hoặc danh sách chương
            return RedirectToAction("Manage", "Choice", new { chapterId = chapter.Id });
        }

        [AllowAnonymous]
        public async Task<IActionResult> Read(int id)
        {
            var chapter = await _context.Chapters
                .Include(c => c.Choices)
                .Include(c => c.Story)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (chapter == null) return NotFound();

            return View(chapter);
        }

        [Authorize]
        public async Task<IActionResult> Manage(int storyId)
        {
            var story = await _context.Stories
                .Include(s => s.Chapters)
                .FirstOrDefaultAsync(s => s.Id == storyId);

            if (story == null)
                return NotFound();

            ViewBag.Story = story;
            return View(story.Chapters.OrderBy(c => c.Id).ToList());
        }

        [Authorize]
        public async Task<IActionResult> Tree(int storyId)
        {
            var chapters = await _context.Chapters
                .Where(c => c.StoryId == storyId)
                .OrderBy(c => c.Id)
                .ToListAsync();

            var chapterDict = chapters.ToDictionary(c => c.Id, c => new ChapterTreeViewModel
            {
                Id = c.Id,
                ContentPreview = c.Content.Length > 50 ? c.Content.Substring(0, 50) + "..." : c.Content,
                CreatedAt = c.CreatedAt
            });

            foreach (var chapter in chapters)
            {
                if (chapter.ParentChapterId.HasValue)
                {
                    chapterDict[chapter.ParentChapterId.Value].Children.Add(chapterDict[chapter.Id]);
                }
            }

            var roots = chapterDict.Values.Where(c =>
                !chapters.Any(ch => ch.Id == c.Id && ch.ParentChapterId.HasValue)).ToList();

            ViewBag.StoryId = storyId;
            return View(roots);
        }
    }
}
