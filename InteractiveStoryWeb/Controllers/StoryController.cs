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
    public class StoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public StoryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        // GET: Story/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Genres = await _context.Genres.ToListAsync();
            return View();
        }

        // POST: Story/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StoryCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Genres = await _context.Genres.ToListAsync();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);

            string imagePath = null;

            if (model.CoverImage != null && model.CoverImage.Length > 0)
            {
                var uploads = Path.Combine(_environment.WebRootPath, "uploads/covers");
                Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.CoverImage.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.CoverImage.CopyToAsync(stream);
                }

                imagePath = "/uploads/covers/" + fileName;
            }

            var story = new Story
            {
                Title = model.Title,
                Genre = model.Genre,
                CoverImageUrl = imagePath,
                IsPublic = model.IsPublic,
                CreatedAt = DateTime.Now,
                AuthorId = user.Id
            };

            _context.Stories.Add(story);
            await _context.SaveChangesAsync();

            // Sau khi tạo truyện, chuyển tới tạo chương đầu tiên
            return RedirectToAction("Create", "Chapter", new { storyId = story.Id });
        }

        [AllowAnonymous]
        public async Task<IActionResult> Read(int id)
        {
            var chapter = await _context.Chapters
                .Where(c => c.StoryId == id && c.ParentChapterId == null && c.CreatedAt > new DateTime(2000, 1, 1))
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (chapter == null)
                return NotFound("Truyện chưa có chương đầu tiên.");

            // ✅ Tăng ViewCount
            var story = await _context.Stories.FindAsync(id);
            if (story != null)
            {
                story.ViewCount++;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Read", "Chapter", new { id = chapter.Id });
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var stories = await _context.Stories
                .Where(s => s.IsPublic)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return View(stories);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var story = await _context.Stories
                .Include(s => s.Author)
                .Include(s => s.Chapters)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (story == null || (!story.IsPublic && !User.Identity.IsAuthenticated))
            {
                return NotFound();
            }

            return View(story);
        }
    }
}
