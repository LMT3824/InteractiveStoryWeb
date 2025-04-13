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
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin.";
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
                Description = model.Description,
                Genre = model.Genre,
                CoverImageUrl = imagePath,
                IsPublic = model.IsPublic,
                CreatedAt = DateTime.Now,
                AuthorId = user.Id
            };

            _context.Stories.Add(story);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Truyện đã được tạo thành công! Tiếp tục tạo chương.";
            return RedirectToAction("Create", "Chapter", new { storyId = story.Id });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var story = await _context.Stories.FindAsync(id);
            if (story == null) return NotFound();

            ViewBag.Genres = await _context.Genres.ToListAsync();
            return View(story);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Story model, IFormFile? NewCoverImage)
        {
            // Xóa lỗi cho các trường không cần thiết
            ModelState.Remove("CreatedAt");
            ModelState.Remove("AuthorId");
            ModelState.Remove("Author");
            ModelState.Remove("Chapters");

            if (!ModelState.IsValid)
            {
                ViewBag.Genres = await _context.Genres.ToListAsync();
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin.";
                return View(model);
            }

            var story = await _context.Stories.FindAsync(model.Id);
            if (story == null) return NotFound();

            if (model.IsPublic)
            {
                var hasValidPublicChapter = await _context.Chapters
                    .Include(c => c.Segments)
                    .AnyAsync(c => c.StoryId == model.Id && c.IsPublic && c.Segments.Any());

                if (!hasValidPublicChapter)
                {
                    ViewBag.Genres = await _context.Genres.ToListAsync();
                    ModelState.AddModelError(string.Empty, "Truyện cần ít nhất một chương được công khai và có ít nhất một đoạn để được phép hiển thị công khai.");
                    TempData["ErrorMessage"] = "Không thể công khai truyện do thiếu chương hợp lệ.";
                    return View(model);
                }
            }

            story.Title = model.Title;
            story.Description = model.Description;
            story.Genre = model.Genre;
            story.IsPublic = model.IsPublic;
            story.AuthorId = model.AuthorId;
            story.CreatedAt = story.CreatedAt;
            story.UpdatedAt = DateTime.Now;

            if (NewCoverImage != null && NewCoverImage.Length > 0)
            {
                var uploads = Path.Combine(_environment.WebRootPath, "uploads/stories");
                Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(NewCoverImage.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await NewCoverImage.CopyToAsync(stream);
                }

                story.CoverImageUrl = "/uploads/stories/" + fileName;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Truyện đã được cập nhật thành công!";
            return RedirectToAction("Details", new { id = story.Id });
        }

        [AllowAnonymous]
        public async Task<IActionResult> Read(int id)
        {
            var story = await _context.Stories
                .FirstOrDefaultAsync(s => s.Id == id && s.IsPublic);

            if (story == null)
                return NotFound("Truyện không tồn tại hoặc chưa được công khai.");

            var firstSegment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                .Where(s => s.Chapter.StoryId == story.Id && s.Chapter.IsPublic)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (firstSegment == null)
                return Content("Truyện chưa có nội dung để đọc.");

            if (firstSegment.Chapter.StoryId != story.Id)
                return NotFound("Lỗi: Đoạn không thuộc truyện này.");

            return RedirectToAction("InteractiveRead", "Segment", new { id = firstSegment.Id });
        }



        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var stories = await _context.Stories
                .Where(s => s.IsPublic)
                .Include(s => s.Chapters)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            // Tạo dictionary để lưu tổng ViewCount cho từng Story
            var viewCounts = new Dictionary<int, int>();
            foreach (var story in stories)
            {
                viewCounts[story.Id] = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;
            }

            ViewBag.ViewCounts = viewCounts; // Truyền dictionary vào ViewBag

            return View(stories);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var story = await _context.Stories
                .Include(s => s.Author)
                .Include(s => s.Chapters)
                    .ThenInclude(ch => ch.Segments)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (story == null)
                return NotFound();

            var firstSegment = story.Chapters
                .Where(ch => ch.IsPublic && ch.Segments != null && ch.Segments.Any())
                .OrderBy(ch => ch.CreatedAt)
                .SelectMany(ch => ch.Segments)
                .OrderBy(s => s.Id)
                .FirstOrDefault();

            // Tính tổng ViewCount từ các Chapter
            var totalViewCount = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;

            ViewBag.FirstSegmentId = firstSegment?.Id;
            ViewBag.TotalViewCount = totalViewCount; // Truyền tổng ViewCount vào ViewBag
            return View(story);
        }
    }
}
