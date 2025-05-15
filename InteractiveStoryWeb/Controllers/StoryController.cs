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
            // Chuẩn hóa ký tự xuống dòng trước khi validation
            if (!string.IsNullOrEmpty(model.Description))
            {
                model.NormalizedDescription = model.Description;
            }

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
                IsPublic = false, // Mặc định không công khai
                AllowCustomization = model.AllowCustomization,
                CreatedAt = DateTime.Now,
                AuthorId = user.Id
            };

            _context.Stories.Add(story);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Truyện đã được tạo thành công! Tiếp tục quản lý chương.";
            return RedirectToAction("Manage", "Chapter", new { storyId = story.Id });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            
            var story = await _context.Stories.FindAsync(id);
            if (story == null) return NotFound();

            ViewBag.Genres = await _context.Genres.ToListAsync();
            ViewBag.CoverImageUrl = story.CoverImageUrl;
            var model = new StoryCreateViewModel
            {
                Id = story.Id,
                Title = story.Title,
                Description = story.Description,
                Genre = story.Genre,
                IsPublic = story.IsPublic,
                AllowCustomization = story.AllowCustomization,
                IsCompleted = story.IsCompleted
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(StoryCreateViewModel model)
        {
            // Chuẩn hóa ký tự xuống dòng trước khi validation
            if (!string.IsNullOrEmpty(model.Description))
            {
                model.NormalizedDescription = model.Description;
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Genres = await _context.Genres.ToListAsync();
                var storyForCover = await _context.Stories.FindAsync(model.Id);
                ViewBag.CoverImageUrl = storyForCover?.CoverImageUrl;
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin.";
                return View(model);
            }

            var story = await _context.Stories.FindAsync(model.Id);
            if (story == null) return NotFound();

            // Kiểm tra trạng thái chương công khai
            var hasValidPublicChapter = await _context.Chapters
                .Include(c => c.Segments)
                .AnyAsync(c => c.StoryId == model.Id && c.IsPublic && c.Segments.Any());

            if (model.IsPublic && !hasValidPublicChapter)
            {
                ViewBag.Genres = await _context.Genres.ToListAsync();
                ViewBag.CoverImageUrl = story.CoverImageUrl;
                TempData["ErrorMessage"] = "Truyện cần ít nhất một chương được công khai và có ít nhất một đoạn để được phép hiển thị công khai.";
                return View(model);
            }

            // Nếu không có chương công khai, buộc truyện thành không công khai
            if (!hasValidPublicChapter)
            {
                model.IsPublic = false;
            }

            story.Title = model.Title;
            story.Description = model.Description;
            story.Genre = model.Genre;
            story.IsPublic = model.IsPublic;
            story.AllowCustomization = model.AllowCustomization;
            story.IsCompleted = model.IsCompleted;
            story.UpdatedAt = DateTime.Now;

            if (model.CoverImage != null && model.CoverImage.Length > 0)
            {
                var uploads = Path.Combine(_environment.WebRootPath, "uploads/stories");
                Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.CoverImage.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.CoverImage.CopyToAsync(stream);
                }

                // Xóa ảnh bìa cũ nếu tồn tại
                if (!string.IsNullOrEmpty(story.CoverImageUrl))
                {
                    var oldImagePath = Path.Combine(_environment.WebRootPath, story.CoverImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                story.CoverImageUrl = "/uploads/stories/" + fileName;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Truyện đã được cập nhật thành công!";
            return RedirectToAction("MyProfile", "Account");
        }

        [AllowAnonymous]
        public async Task<IActionResult> Read(int id)
        {
            var story = await _context.Stories
                .FirstOrDefaultAsync(s => s.Id == id && s.IsPublic && !s.Author.IsBanned && !s.IsHidden);

            if (story == null)
                return NotFound("Truyện không tồn tại, chưa được công khai hoặc đã bị ẩn.");

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
        public async Task<IActionResult> Index(string genre)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            string currentUserId = currentUser?.Id;

            var query = _context.Stories
                .Where(s => s.IsPublic && !s.Author.IsBanned && !s.IsHidden
                    && (string.IsNullOrEmpty(genre) || (s.Genre != null && s.Genre.ToLower() == genre.ToLower()))
                    && (currentUserId == null || !_context.Blocks.Any(b => b.UserId == currentUserId && (b.BlockedUserId == s.AuthorId || b.BlockedStoryId == s.Id))))
                .Include(s => s.Chapters)
                .OrderBy(s => s.CreatedAt);

            var stories = await query.ToListAsync();

            var viewCounts = new Dictionary<int, int>();
            var ratings = new Dictionary<int, double>();
            var chapterCounts = new Dictionary<int, int>();

            foreach (var story in stories)
            {
                viewCounts[story.Id] = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;
                var storyRatings = await _context.Ratings
                    .Where(r => r.StoryId == story.Id && !r.User.IsBanned
                        && (currentUserId == null || !_context.Blocks.Any(b => b.UserId == currentUserId && b.BlockedUserId == r.UserId)))
                    .ToListAsync();
                ratings[story.Id] = storyRatings.Any() ? storyRatings.Average(r => r.RatingValue) : 0;
                chapterCounts[story.Id] = story.Chapters?.Count ?? 0;
            }

            ViewBag.ViewCounts = viewCounts;
            ViewBag.Ratings = ratings;
            ViewBag.ChapterCounts = chapterCounts;

            return View(stories);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details(int id, string errorMessage = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            string currentUserId = currentUser?.Id;

            // Kiểm tra trạng thái chặn trước
            bool isStoryBlocked = false;
            bool isAuthorBlocked = false;
            if (currentUserId != null)
            {
                isStoryBlocked = await _context.Blocks
                    .AnyAsync(b => b.UserId == currentUserId && b.BlockedStoryId == id);
                isAuthorBlocked = await _context.Stories
                    .Where(s => s.Id == id)
                    .AnyAsync(s => _context.Blocks.Any(b => b.UserId == currentUserId && b.BlockedUserId == s.AuthorId));
            }

            // Nếu truyện hoặc tác giả bị chặn, trả về view với thông báo
            if (isStoryBlocked || isAuthorBlocked)
            {
                ViewBag.IsStoryBlocked = isStoryBlocked;
                ViewBag.IsAuthorBlocked = isAuthorBlocked;
                ViewBag.CurrentUser = currentUser;
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    TempData["ErrorMessage"] = errorMessage;
                }
                return View(new Story { Id = id }); // Model giả để render view
            }

            // Lấy truyện nếu không bị chặn
            var story = await _context.Stories
                .Include(s => s.Author)
                .Include(s => s.Chapters)
                    .ThenInclude(ch => ch.Segments)
                .FirstOrDefaultAsync(s => s.Id == id && !s.Author.IsBanned && !s.IsHidden
                    && (currentUserId == null ||
                        (!_context.Blocks.Any(b => b.UserId == currentUserId && (b.BlockedUserId == s.AuthorId || b.BlockedStoryId == s.Id)) &&
                         !_context.Blocks.Any(b => b.UserId == s.AuthorId && b.BlockedUserId == currentUserId))));

            if (story == null)
            {
                // Kiểm tra xem truyện có bị ẩn không
                var isHidden = await _context.Stories.AnyAsync(s => s.Id == id && s.IsHidden);
                if (isHidden)
                {
                    ViewBag.IsHidden = true;
                    ViewBag.CurrentUser = currentUser;
                    return View(new Story { Id = id }); // Model giả để render view
                }
                return NotFound("Truyện không tồn tại.");
            }

            var firstSegment = story.Chapters
                .Where(ch => ch.IsPublic && ch.Segments != null && ch.Segments.Any())
                .OrderBy(ch => ch.CreatedAt)
                .SelectMany(ch => ch.Segments)
                .OrderBy(s => s.Id)
                .FirstOrDefault();

            var totalViewCount = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;

            var comments = await _context.Comments
                .Where(c => c.StoryId == id && !c.User.IsBanned
                    && (currentUserId == null ||
                        (!_context.Blocks.Any(b => b.UserId == currentUserId && b.BlockedUserId == c.UserId) &&
                         !_context.Blocks.Any(b => b.UserId == c.UserId && b.BlockedUserId == currentUserId))))
                .Include(c => c.User)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var ratings = await _context.Ratings
                .Where(r => r.StoryId == id && !r.User.IsBanned
                    && (currentUserId == null ||
                        (!_context.Blocks.Any(b => b.UserId == currentUserId && b.BlockedUserId == r.UserId) &&
                         !_context.Blocks.Any(b => b.UserId == r.UserId && b.BlockedUserId == currentUserId))))
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            bool isInLibrary = false;
            if (currentUser != null)
            {
                isInLibrary = await _context.Libraries
                    .AnyAsync(l => l.UserId == currentUser.Id && l.StoryId == id);
            }

            ViewBag.FirstSegmentId = firstSegment?.Id;
            ViewBag.TotalViewCount = totalViewCount;
            ViewBag.Comments = comments;
            ViewBag.Ratings = ratings;
            ViewBag.AverageRating = ratings.Any() ? ratings.Average(r => r.RatingValue) : 0;
            ViewBag.CurrentUser = currentUser;
            ViewBag.IsInLibrary = isInLibrary;
            ViewBag.IsStoryBlocked = isStoryBlocked;

            if (!string.IsNullOrEmpty(errorMessage))
            {
                TempData["ErrorMessage"] = errorMessage;
            }

            return View(story);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return PartialView("_SearchResults", new List<Story>());
            }

            var stories = await _context.Stories
                .Where(s => s.IsPublic && !s.IsHidden &&
                           (s.Title.Contains(query) ||
                            s.Author != null && s.Author.UserName.Contains(query) && !s.Author.IsBanned))
                .Include(s => s.Author)
                .Include(s => s.Chapters)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var viewCounts = new Dictionary<int, int>();
            foreach (var story in stories)
            {
                viewCounts[story.Id] = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;
            }

            ViewBag.ViewCounts = viewCounts;
            ViewBag.Query = query;
            return PartialView("_SearchResults", stories);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Customize(int storyId)
        {
            Console.WriteLine($"Customize GET called with storyId: {storyId}");

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
                    Console.WriteLine($"Retrieved customization from Session for storyId {storyId}: {sessionData}");
                }
                else
                {
                    Console.WriteLine($"No customization found in Session for storyId {storyId}");
                }
            }

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Customize(StoryCustomizationViewModel model)
        {
            Console.WriteLine($"Customize POST called with storyId: {model.StoryId}");

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
                    _context.ReaderStoryCustomizations.Update(existingCustomization);
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
                var sessionData = JsonSerializer.Serialize(customization);
                HttpContext.Session.SetString(sessionKey, sessionData);
                Console.WriteLine($"Saved customization to Session for storyId {model.StoryId}: {sessionData}");
            }

            // Lấy đoạn đầu tiên của truyện để chuyển hướng
            var firstChapter = await _context.Chapters
                .Where(c => c.StoryId == model.StoryId && c.IsPublic)
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (firstChapter == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy chương nào trong truyện.";
                return RedirectToAction("Details", "Story", new { id = model.StoryId });
            }

            var firstSegment = await _context.ChapterSegments
                .Where(s => s.ChapterId == firstChapter.Id && s.Chapter.IsPublic)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (firstSegment == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đoạn nào trong chương đầu tiên.";
                return RedirectToAction("Details", "Story", new { id = model.StoryId });
            }

            TempData["SuccessMessage"] = "Thông tin tùy chỉnh đã được lưu!";
            return RedirectToAction("InteractiveRead", "Segment", new { id = firstSegment.Id }); // Chuyển hướng với segmentId
        }

        // Action mới để định dạng đoạn văn bản xem trước
        [HttpPost]
        [AllowAnonymous]
        public IActionResult PreviewCustomization(string name, string firstPersonPronoun, string secondPersonPronoun)
        {
            // Đoạn văn bản mặc định
            string previewText1 =
                 "\"Xin chào, tên của [XưngHôThứNhất] là [Tên]. Rất vui được gặp mặt.\"\n" +
                 "[XưngHôThứHai] đặt tay lên ngực, lịch sự cúi đầu chào đối phương. Đối phương vui vẻ đáp lại lời chào của [XưngHôThứHai].\n" +
                 "\"Tôi cũng vậy, rất vui khi được quen biết [Tên].\"";

            string previewText2 =
                "\"Anh đến rồi à?\"\n" +
                "[Tên] ngẩng đầu lên khi nghe tiếng bước chân quen thuộc. Trong ánh sáng nhạt, khuôn mặt của anh ta hiện ra rõ ràng, vẫn ánh mắt đó – ánh mắt khiến [XưngHôThứHai] không thể nào quên.\n" +
                "\"[XưngHôThứNhất] đang đợi anh đấy. Tưởng anh lạc trôi xó nào rồi, [XưngHôThứNhất] còn đang tính bỏ về đây.\" Giọng của [Tên] mang ý trách móc nhẹ đối phương.\n" +
                "Anh khẽ cười, chỉ bước lại gần, đưa tay chạm nhẹ vào vai [Tên].\n" +
                "\"Xin lỗi nhé, may là tôi tới kịp lúc trước khi khiến [XưngHôThứHai] đây mất hết sự kiên nhẫn.\"\n" +
                "[Tên] hừ nhẹ."; ;

            // Thay thế các placeholder bằng TextFormatter
            // Thay thế các placeholder bằng TextFormatter cho đoạn 1
            previewText1 = TextFormatter.ReplaceWithContextualCapitalization(previewText1, "[Tên]", name ?? "[Tên]");
            previewText1 = TextFormatter.ReplaceWithContextualCapitalization(previewText1, "[XưngHôThứNhất]", firstPersonPronoun ?? "[XưngHôThứNhất]");
            previewText1 = TextFormatter.ReplaceWithContextualCapitalization(previewText1, "[XưngHôThứHai]", secondPersonPronoun ?? "[XưngHôThứHai]");

            // Thay thế các placeholder bằng TextFormatter cho đoạn 2
            previewText2 = TextFormatter.ReplaceWithContextualCapitalization(previewText2, "[Tên]", name ?? "[Tên]");
            previewText2 = TextFormatter.ReplaceWithContextualCapitalization(previewText2, "[XưngHôThứNhất]", firstPersonPronoun ?? "[XưngHôThứNhất]");
            previewText2 = TextFormatter.ReplaceWithContextualCapitalization(previewText2, "[XưngHôThứHai]", secondPersonPronoun ?? "[XưngHôThứHai]");

            // Tách các đoạn để trả về dưới dạng danh sách
            var paragraphs1 = previewText1.Split('\n');
            var paragraphs2 = previewText2.Split('\n');

            return Json(new { success = true, paragraphs1 = paragraphs1, paragraphs2 = paragraphs2 });
        }

        public IActionResult TOS()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var story = await _context.Stories
                    .Include(s => s.Chapters)
                        .ThenInclude(c => c.Segments)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (story == null)
                {
                    return Json(new { success = false, message = "Truyện không tồn tại." });
                }

                var user = await _userManager.GetUserAsync(User);
                if (story.AuthorId != user.Id)
                {
                    return Json(new { success = false, message = "Bạn không có quyền xóa truyện này." });
                }

                // Tìm tất cả các ChapterSegment thuộc Story
                var segmentIds = story.Chapters
                    .SelectMany(c => c.Segments)
                    .Select(s => s.Id)
                    .ToList();

                // Tìm tất cả các Choices có NextSegmentId tham chiếu đến các ChapterSegment của Story
                var choicesReferencingSegments = await _context.Choices
                    .Where(c => segmentIds.Contains(c.NextSegmentId))
                    .ToListAsync();

                // Xóa các Choices này để tránh xung đột ràng buộc
                _context.Choices.RemoveRange(choicesReferencingSegments);

                // Đặt ChapterSegmentId trong ReadingProgress thành null trước khi xóa
                var relatedProgresses = await _context.ReadingProgresses
                    .Where(rp => segmentIds.Contains(rp.ChapterSegmentId.Value))
                    .ToListAsync();

                foreach (var progress in relatedProgresses)
                {
                    progress.ChapterSegmentId = null;
                }

                _context.Stories.Remove(story);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Truyện đã được xóa thành công!",
                    storyId = id
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Có lỗi xảy ra khi xóa truyện: {ex.Message}"
                });
            }
        }

        [AllowAnonymous]
        public async Task<IActionResult> Comments(int storyId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            string currentUserId = currentUser?.Id;

            var story = await _context.Stories
                .Include(s => s.Author)
                .FirstOrDefaultAsync(s => s.Id == storyId && !s.Author.IsBanned && !s.IsHidden
                    && (currentUserId == null || !_context.Blocks.Any(b => b.UserId == currentUserId && (b.BlockedUserId == s.AuthorId || b.BlockedStoryId == s.Id))));

            if (story == null)
            {
                return NotFound("Truyện không tồn tại, tác giả đã bị chặn, bạn đã chặn truyện này, hoặc truyện đã bị ẩn.");
            }

            var comments = await _context.Comments
                .Where(c => c.StoryId == storyId && !c.User.IsBanned
                    && (currentUserId == null || !_context.Blocks.Any(b => b.UserId == currentUserId && b.BlockedUserId == c.UserId)
                    && !_context.Blocks.Any(b => b.UserId == c.UserId && b.BlockedUserId == currentUserId)))
                .Include(c => c.User)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            ViewBag.Comments = comments;
            ViewBag.CurrentUser = currentUser;
            return View(story);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddComment(int storyId, string content)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var story = await _context.Stories.FindAsync(storyId);
            if (story == null)
            {
                return Json(new { success = false, message = "Truyện không tồn tại." });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, message = "Nội dung bình luận không được để trống." });
            }

            var comment = new Comment
            {
                UserId = user.Id,
                StoryId = storyId,
                Content = content,
                CreatedAt = DateTime.Now
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                commentId = comment.Id,
                userName = user.UserName,
                content = comment.Content,
                createdAt = comment.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                updatedAt = comment.UpdatedAt?.ToString("dd/MM/yyyy HH:mm"), // Thêm UpdatedAt vào phản hồi
                avatarUrl = string.IsNullOrEmpty(user.AvatarUrl) ? "/images/AvatarNotFound.png" : user.AvatarUrl
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> EditComment(int id, string content)
        {
            var comment = await _context.Comments
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null)
            {
                return Json(new { success = false, message = "Bình luận không tồn tại." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (comment.UserId != user.Id)
            {
                return Json(new { success = false, message = "Bạn không có quyền chỉnh sửa bình luận này." });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, message = "Nội dung bình luận không được để trống." });
            }

            comment.Content = content;
            comment.UpdatedAt = DateTime.Now; // Cập nhật UpdatedAt khi chỉnh sửa
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                content = comment.Content,
                createdAt = comment.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                updatedAt = comment.UpdatedAt?.ToString("dd/MM/yyyy HH:mm") // Thêm UpdatedAt vào phản hồi
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var comment = await _context.Comments
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null)
            {
                return Json(new { success = false, message = "Bình luận không tồn tại." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (comment.UserId != user.Id)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa bình luận này." });
            }

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            var ratings = await _context.Ratings
                .Where(r => r.StoryId == comment.StoryId)
                .ToListAsync();
            var averageRating = ratings.Any() ? ratings.Average(r => r.RatingValue) : 0;

            return Json(new { success = true, averageRating = averageRating });
        }

        // Actions for Ratings
        [AllowAnonymous]
        public async Task<IActionResult> Ratings(int storyId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            string currentUserId = currentUser?.Id;

            var story = await _context.Stories
                .Include(s => s.Author)
                .FirstOrDefaultAsync(s => s.Id == storyId && !s.Author.IsBanned && !s.IsHidden
                    && (currentUserId == null || !_context.Blocks.Any(b => b.UserId == currentUserId && (b.BlockedUserId == s.AuthorId || b.BlockedStoryId == s.Id))));

            if (story == null)
            {
                return NotFound("Truyện không tồn tại, tác giả đã bị chặn, bạn đã chặn truyện này, hoặc truyện đã bị ẩn.");
            }

            var ratings = await _context.Ratings
                .Where(r => r.StoryId == storyId && !r.User.IsBanned
                    && (currentUserId == null || !_context.Blocks.Any(b => b.UserId == currentUserId && b.BlockedUserId == r.UserId)
                    && !_context.Blocks.Any(b => b.UserId == r.UserId && b.BlockedUserId == currentUserId)))
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.Ratings = ratings;
            ViewBag.AverageRating = ratings.Any() ? ratings.Average(r => r.RatingValue) : 0;
            ViewBag.CurrentUser = currentUser;
            return View(story);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddRating(int storyId, int ratingValue)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var story = await _context.Stories.FindAsync(storyId);
            if (story == null)
            {
                return Json(new { success = false, message = "Truyện không tồn tại." });
            }

            if (ratingValue < 1 || ratingValue > 5)
            {
                return Json(new { success = false, message = "Điểm đánh giá phải từ 1 đến 5 sao." });
            }

            var existingRating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.UserId == user.Id && r.StoryId == storyId);

            if (existingRating != null)
            {
                return Json(new { success = false, message = "Bạn đã đánh giá truyện này rồi. Vui lòng chỉnh sửa đánh giá hiện có." });
            }

            var rating = new Rating
            {
                UserId = user.Id,
                StoryId = storyId,
                RatingValue = ratingValue
            };

            _context.Ratings.Add(rating);
            await _context.SaveChangesAsync();

            var ratings = await _context.Ratings
                .Where(r => r.StoryId == storyId)
                .ToListAsync();
            var averageRating = ratings.Any() ? ratings.Average(r => r.RatingValue) : 0;

            return Json(new
            {
                success = true,
                ratingId = rating.Id,
                userId = user.Id, // Thêm userId
                userName = user.UserName,
                ratingValue = rating.RatingValue,
                createdAt = rating.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                averageRating = averageRating,
                avatarUrl = string.IsNullOrEmpty(user.AvatarUrl) ? "/images/AvatarNull.jpg" : user.AvatarUrl // Thêm avatarUrl
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> EditRating(int id, int ratingValue)
        {
            var rating = await _context.Ratings
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rating == null)
            {
                return Json(new { success = false, message = "Đánh giá không tồn tại." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (rating.UserId != user.Id)
            {
                return Json(new { success = false, message = "Bạn không có quyền chỉnh sửa đánh giá này." });
            }

            if (ratingValue < 1 || ratingValue > 5)
            {
                return Json(new { success = false, message = "Điểm đánh giá phải từ 1 đến 5 sao." });
            }

            rating.RatingValue = ratingValue;
            rating.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var ratings = await _context.Ratings
                .Where(r => r.StoryId == rating.StoryId)
                .ToListAsync();
            var averageRating = ratings.Any() ? ratings.Average(r => r.RatingValue) : 0;

            return Json(new
            {
                success = true,
                userId = user.Id, // Thêm userId
                ratingValue = rating.RatingValue,
                averageRating = averageRating,
                createdAt = rating.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                updatedAt = rating.UpdatedAt?.ToString("dd/MM/yyyy HH:mm")
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> DeleteRating(int id)
        {
            var rating = await _context.Ratings
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rating == null)
            {
                return Json(new { success = false, message = "Đánh giá không tồn tại." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (rating.UserId != user.Id)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa đánh giá này." });
            }

            _context.Ratings.Remove(rating);
            await _context.SaveChangesAsync();

            var ratings = await _context.Ratings
                .Where(r => r.StoryId == rating.StoryId)
                .ToListAsync();
            var averageRating = ratings.Any() ? ratings.Average(r => r.RatingValue) : 0;

            return Json(new { success = true, averageRating = averageRating });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddToLibrary(int storyId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var story = await _context.Stories.FindAsync(storyId);
            if (story == null)
            {
                return Json(new { success = false, message = "Truyện không tồn tại." });
            }

            // Kiểm tra xem truyện đã có trong thư viện chưa
            var existingEntry = await _context.Libraries
                .FirstOrDefaultAsync(l => l.UserId == user.Id && l.StoryId == storyId);

            if (existingEntry != null)
            {
                return Json(new { success = false, message = "Truyện đã có trong thư viện." });
            }

            var libraryEntry = new Library
            {
                UserId = user.Id,
                StoryId = storyId
            };

            _context.Libraries.Add(libraryEntry);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã thêm truyện vào thư viện!" });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> RemoveFromLibrary(int storyId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var libraryEntry = await _context.Libraries
                .FirstOrDefaultAsync(l => l.UserId == user.Id && l.StoryId == storyId);

            if (libraryEntry == null)
            {
                return Json(new { success = false, message = "Truyện không có trong thư viện." });
            }

            _context.Libraries.Remove(libraryEntry);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa truyện khỏi thư viện!" });
        }

        [Authorize]
        public async Task<IActionResult> Library()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("Người dùng không tồn tại.");
            }

            var libraryEntries = await _context.Libraries
                .Where(l => l.UserId == user.Id)
                .Include(l => l.Story)
                    .ThenInclude(s => s.Author)
                .Include(l => l.Story)
                    .ThenInclude(s => s.Chapters)
                        .ThenInclude(c => c.Segments)
                .Where(l => !l.Story.IsHidden
                    && !_context.Blocks.Any(b => b.UserId == user.Id && b.BlockedStoryId == l.StoryId)) // Loại bỏ truyện bị chặn
                .ToListAsync();

            var readingProgresses = await _context.ReadingProgresses
                .Where(rp => rp.UserId == user.Id)
                .Include(rp => rp.Story)
                .Include(rp => rp.ChapterSegment)
                    .ThenInclude(cs => cs.Chapter)
                .ToListAsync();

            var viewCounts = new Dictionary<int, int>();
            var ratings = new Dictionary<int, double>();
            var chapterCounts = new Dictionary<int, int>();

            foreach (var entry in libraryEntries)
            {
                var story = entry.Story;
                // Tính tổng lượt xem
                viewCounts[story.Id] = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;
                // Tính đánh giá trung bình
                ratings[story.Id] = _context.Ratings.Where(r => r.StoryId == story.Id).Any()
                    ? _context.Ratings.Where(r => r.StoryId == story.Id).Average(r => r.RatingValue)
                    : 0;
                // Tính số chương công khai
                chapterCounts[story.Id] = story.Chapters?.Count(ch => ch.IsPublic) ?? 0;
            }

            ViewBag.ReadingProgresses = readingProgresses;
            ViewBag.ViewCounts = viewCounts;
            ViewBag.Ratings = ratings;
            ViewBag.ChapterCounts = chapterCounts;

            return View(libraryEntries);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockStory(int storyId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var story = await _context.Stories.FindAsync(storyId);
            if (story == null)
            {
                return Json(new { success = false, message = "Truyện không tồn tại." });
            }

            // Kiểm tra xem đã chặn truyện chưa
            var existingBlock = await _context.Blocks
                .FirstOrDefaultAsync(b => b.UserId == currentUser.Id && b.BlockedStoryId == storyId);

            if (existingBlock != null)
            {
                return Json(new { success = false, message = "Bạn đã chặn truyện này." });
            }

            var block = new Block
            {
                UserId = currentUser.Id,
                BlockedStoryId = storyId
            };

            _context.Blocks.Add(block);

            // Xóa truyện khỏi thư viện nếu đã lưu
            var libraryEntry = await _context.Libraries
                .FirstOrDefaultAsync(l => l.UserId == currentUser.Id && l.StoryId == storyId);
            bool removedFromLibrary = false;
            if (libraryEntry != null)
            {
                _context.Libraries.Remove(libraryEntry);
                removedFromLibrary = true;
            }

            await _context.SaveChangesAsync();

            var message = removedFromLibrary
                ? "Đã chặn truyện và xóa khỏi thư viện!"
                : "Đã chặn truyện!";

            return Json(new { success = true, message = message });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnblockStory(int storyId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var block = await _context.Blocks
                .FirstOrDefaultAsync(b => b.UserId == currentUser.Id && b.BlockedStoryId == storyId);

            if (block == null)
            {
                return Json(new { success = false, message = "Bạn chưa chặn truyện này." });
            }

            _context.Blocks.Remove(block);
            await _context.SaveChangesAsync();

            // Lấy lại thông tin truyện sau khi bỏ chặn
            var story = await _context.Stories
                .Include(s => s.Author)
                .Include(s => s.Chapters)
                    .ThenInclude(ch => ch.Segments)
                .FirstOrDefaultAsync(s => s.Id == storyId && !s.Author.IsBanned);

            if (story == null)
            {
                return Json(new { success = false, message = "Truyện không tồn tại hoặc đã bị xóa." });
            }

            // Lấy lại các thông tin cần thiết
            var comments = await _context.Comments
                .Where(c => c.StoryId == storyId && !c.User.IsBanned)
                .Include(c => c.User)
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .ToListAsync();

            var ratings = await _context.Ratings
                .Where(r => r.StoryId == storyId && !r.User.IsBanned)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .ToListAsync();

            var averageRating = ratings.Any() ? ratings.Average(r => r.RatingValue) : 0;
            var totalViewCount = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;
            var firstSegment = story.Chapters?
                .Where(ch => ch.IsPublic && ch.Segments != null && ch.Segments.Any())
                .OrderBy(ch => ch.CreatedAt)
                .SelectMany(ch => ch.Segments)
                .OrderBy(s => s.Id)
                .FirstOrDefault();

            return Json(new
            {
                success = true,
                message = "Đã bỏ chặn truyện!",
                story = new
                {
                    id = story.Id,
                    title = story.Title,
                    description = story.Description,
                    genre = story.Genre,
                    coverImageUrl = story.CoverImageUrl,
                    isPublic = story.IsPublic,
                    allowCustomization = story.AllowCustomization,
                    createdAt = story.CreatedAt.ToString("dd/MM/yyyy"),
                    author = new
                    {
                        id = story.Author.Id,
                        userName = story.Author.UserName
                    },
                    chapters = story.Chapters?.Select(ch => new
                    {
                        id = ch.Id,
                        title = ch.Title,
                        createdAt = ch.CreatedAt.ToString("dd/MM/yyyy"),
                        viewCount = ch.ViewCount,
                        isPublic = ch.IsPublic,
                        segments = ch.Segments?.Select(s => new
                        {
                            id = s.Id,
                            title = s.Title
                        }).ToList()
                    }).ToList()
                },
                comments = comments.Select(c => new
                {
                    id = c.Id,
                    content = c.Content,
                    createdAt = c.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    updatedAt = c.UpdatedAt?.ToString("dd/MM/yyyy HH:mm"),
                    user = new
                    {
                        id = c.User.Id,
                        userName = c.User.UserName,
                        avatarUrl = string.IsNullOrEmpty(c.User.AvatarUrl) ? "/images/AvatarNull.jpg" : c.User.AvatarUrl
                    }
                }),
                ratings = ratings.Select(r => new
                {
                    id = r.Id,
                    ratingValue = r.RatingValue,
                    createdAt = r.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    updatedAt = r.UpdatedAt?.ToString("dd/MM/yyyy HH:mm"),
                    user = new
                    {
                        id = r.User.Id,
                        userName = r.User.UserName,
                        avatarUrl = string.IsNullOrEmpty(r.User.AvatarUrl) ? "/images/AvatarNull.jpg" : r.User.AvatarUrl
                    }
                }),
                averageRating = averageRating,
                totalViewCount = totalViewCount,
                firstSegmentId = firstSegment?.Id
            });
        }

        // Báo cáo truyện
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportStory(int storyId, string reason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var story = await _context.Stories.FindAsync(storyId);
            if (story == null)
            {
                return Json(new { success = false, message = "Truyện không tồn tại." });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return Json(new { success = false, message = "Lý do báo cáo không được để trống." });
            }

            var existingReport = await _context.Reports
                .FirstOrDefaultAsync(r => r.UserId == user.Id && r.StoryId == storyId && r.CommentId == null);
            if (existingReport != null)
            {
                return Json(new { success = false, message = "Bạn đã báo cáo truyện này rồi." });
            }

            var report = new Report
            {
                UserId = user.Id,
                StoryId = storyId,
                AuthorId = story.AuthorId,
                Reason = reason,
                ReportedAt = DateTime.Now
            };

            _context.Reports.Add(report);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Báo cáo truyện đã được gửi!" });
        }

        // Báo cáo bình luận
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportComment(int commentId, string reason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại." });
            }

            var comment = await _context.Comments.FindAsync(commentId);
            if (comment == null)
            {
                return Json(new { success = false, message = "Bình luận không tồn tại." });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return Json(new { success = false, message = "Lý do báo cáo không được để trống." });
            }

            var existingReport = await _context.Reports
                .FirstOrDefaultAsync(r => r.UserId == user.Id && r.CommentId == commentId);
            if (existingReport != null)
            {
                return Json(new { success = false, message = "Bạn đã báo cáo bình luận này rồi." });
            }

            var report = new Report
            {
                UserId = user.Id,
                CommentId = commentId,
                StoryId = comment.StoryId,
                AuthorId = comment.UserId,
                Reason = reason,
                ReportedAt = DateTime.Now
            };

            _context.Reports.Add(report);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Báo cáo bình luận đã được gửi!" });
        }
    }
}
