using InteractiveStoryWeb.Data;
using InteractiveStoryWeb.Models;
using InteractiveStoryWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
                AllowCustomization = model.AllowCustomization, // Lưu tùy chọn
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
            story.AllowCustomization = model.AllowCustomization; // Cập nhật tùy chọn cá nhân hóa
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

            // Kiểm tra nếu tính năng cá nhân hóa được bật
            if (story.AllowCustomization)
            {
                var user = await _userManager.GetUserAsync(User);
                string userId = user?.Id ?? "anonymous";

                // Kiểm tra xem thông tin tùy chỉnh đã tồn tại chưa
                if (user != null) // Người dùng đã đăng nhập
                {
                    var customization = await _context.ReaderStoryCustomizations
                        .FirstOrDefaultAsync(rsc => rsc.StoryId == id && rsc.UserId == userId);

                    if (customization == null)
                    {
                        return RedirectToAction("Customize", new { storyId = id });
                    }
                }
                else // Người dùng chưa đăng nhập
                {
                    // Kiểm tra trong session
                    var sessionKey = $"Customization_{id}";
                    var sessionData = HttpContext.Session.GetString(sessionKey);
                    if (string.IsNullOrEmpty(sessionData))
                    {
                        return RedirectToAction("Customize", new { storyId = id });
                    }
                }
            }

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

        [AllowAnonymous]
        public async Task<IActionResult> Customize(int storyId)
        {
            var user = await _userManager.GetUserAsync(User);
            string userId = user?.Id ?? "anonymous";

            var model = new StoryCustomizationViewModel { StoryId = storyId };

            if (user != null) // Người dùng đã đăng nhập
            {
                var existingCustomization = await _context.ReaderStoryCustomizations
                    .FirstOrDefaultAsync(rsc => rsc.StoryId == storyId && rsc.UserId == userId);
                if (existingCustomization != null)
                {
                    model.Name = existingCustomization.Name;
                    model.FirstPersonPronoun = existingCustomization.FirstPersonPronoun;
                    model.SecondPersonPronoun = existingCustomization.SecondPersonPronoun;
                }
            }
            else // Người dùng chưa đăng nhập
            {
                var sessionKey = $"Customization_{storyId}";
                var sessionData = HttpContext.Session.GetString(sessionKey);
                if (!string.IsNullOrEmpty(sessionData))
                {
                    var existingCustomization = JsonSerializer.Deserialize<ReaderStoryCustomization>(sessionData);
                    model.Name = existingCustomization.Name;
                    model.FirstPersonPronoun = existingCustomization.FirstPersonPronoun;
                    model.SecondPersonPronoun = existingCustomization.SecondPersonPronoun;
                }
            }

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Customize(StoryCustomizationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin.";
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            string userId = user?.Id ?? "anonymous";

            if (user != null) // Người dùng đã đăng nhập, lưu vào database
            {
                // Kiểm tra xem bản ghi đã tồn tại chưa
                var existingCustomization = await _context.ReaderStoryCustomizations
                    .FirstOrDefaultAsync(rsc => rsc.StoryId == model.StoryId && rsc.UserId == userId);

                if (existingCustomization != null) // Nếu đã tồn tại, cập nhật
                {
                    existingCustomization.Name = model.Name;
                    existingCustomization.FirstPersonPronoun = model.FirstPersonPronoun;
                    existingCustomization.SecondPersonPronoun = model.SecondPersonPronoun;
                    existingCustomization.CreatedAt = existingCustomization.CreatedAt; // Giữ nguyên thời gian tạo
                    _context.ReaderStoryCustomizations.Update(existingCustomization); // Đảm bảo gọi Update
                }
                else // Nếu chưa tồn tại, tạo mới
                {
                    var customization = new ReaderStoryCustomization
                    {
                        UserId = userId,
                        StoryId = model.StoryId,
                        Name = model.Name,
                        FirstPersonPronoun = model.FirstPersonPronoun,
                        SecondPersonPronoun = model.SecondPersonPronoun,
                        CreatedAt = DateTime.Now
                    };
                    _context.ReaderStoryCustomizations.Add(customization);
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Có lỗi xảy ra khi lưu thông tin: " + ex.Message;
                    return View(model);
                }
            }
            else // Người dùng chưa đăng nhập, lưu vào session
            {
                var customization = new ReaderStoryCustomization
                {
                    UserId = userId,
                    StoryId = model.StoryId,
                    Name = model.Name,
                    FirstPersonPronoun = model.FirstPersonPronoun,
                    SecondPersonPronoun = model.SecondPersonPronoun,
                    CreatedAt = DateTime.Now
                };

                // Lưu vào session dưới dạng JSON
                var sessionKey = $"Customization_{model.StoryId}";
                HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(customization));
            }

            TempData["SuccessMessage"] = "Thông tin tùy chỉnh đã được lưu!";
            return RedirectToAction("Read", new { id = model.StoryId });
        }
    }
}
