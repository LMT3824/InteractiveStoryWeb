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
        public async Task<IActionResult> Edit(StoryCreateViewModel model, IFormFile? NewCoverImage)
        {
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

                story.CoverImageUrl = "/Uploads/stories/" + fileName;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Truyện đã được cập nhật thành công!";
            return RedirectToAction("MyProfile", "Account");
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
        public async Task<IActionResult> Index(string genre)
        {
            var query = _context.Stories
                .Where(s => s.IsPublic && (string.IsNullOrEmpty(genre) || (s.Genre != null && s.Genre.ToLower() == genre.ToLower())))
                .Include(s => s.Chapters)
                .OrderBy(s => s.CreatedAt); // Sắp xếp từ cũ nhất đến mới nhất

            var stories = await query.ToListAsync();

            var viewCounts = new Dictionary<int, int>();
            var ratings = new Dictionary<int, double>();
            var chapterCounts = new Dictionary<int, int>();

            foreach (var story in stories)
            {
                viewCounts[story.Id] = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;
                var storyRatings = await _context.Ratings.Where(r => r.StoryId == story.Id).ToListAsync();
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

            var totalViewCount = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;

            var comments = await _context.Comments
                .Where(c => c.StoryId == id)
                .Include(c => c.User)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var ratings = await _context.Ratings
                .Where(r => r.StoryId == id)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ApplicationUser currentUser = null;
            bool isInLibrary = false;
            if (User.Identity.IsAuthenticated)
            {
                currentUser = await _userManager.GetUserAsync(User);
                isInLibrary = await _context.Libraries
                    .AnyAsync(l => l.UserId == currentUser.Id && l.StoryId == id);
            }

            ViewBag.FirstSegmentId = firstSegment?.Id;
            ViewBag.TotalViewCount = totalViewCount;
            ViewBag.Comments = comments;
            ViewBag.Ratings = ratings;
            ViewBag.AverageRating = ratings.Any() ? ratings.Average(r => r.RatingValue) : 0;
            ViewBag.CurrentUser = currentUser;
            ViewBag.IsInLibrary = isInLibrary; // Truyền trạng thái vào ViewBag
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
                .Where(s => s.IsPublic &&
                           (s.Title.Contains(query) ||
                            s.Author.UserName.Contains(query)))
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
            previewText1 = TextFormatter.ReplaceWithContextualCapitalization(previewText1, "[XưngHôThứHai]", secondPersonPronoun ?? "[Xưng Hô Thứ Hai]");

            // Thay thế các placeholder bằng TextFormatter cho đoạn 2
            previewText2 = TextFormatter.ReplaceWithContextualCapitalization(previewText2, "[Tên]", name ?? "[Tên]");
            previewText2 = TextFormatter.ReplaceWithContextualCapitalization(previewText2, "[XưngHôThứNhất]", firstPersonPronoun ?? "[XưngHôThứNhất]");
            previewText2 = TextFormatter.ReplaceWithContextualCapitalization(previewText2, "[XưngHôThứHai]", secondPersonPronoun ?? "[Xưng Hô Thứ Hai]");

            // Tách các đoạn để trả về dưới dạng danh sách
            var paragraphs1 = previewText1.Split('\n');
            var paragraphs2 = previewText2.Split('\n');

            return Json(new { success = true, paragraphs1 = paragraphs1, paragraphs2 = paragraphs2 });
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
            var story = await _context.Stories
                .Include(s => s.Author)
                .FirstOrDefaultAsync(s => s.Id == storyId);

            if (story == null)
                return NotFound("Truyện không tồn tại.");

            var comments = await _context.Comments
                .Where(c => c.StoryId == storyId)
                .Include(c => c.User)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            ApplicationUser currentUser = null;
            if (User.Identity.IsAuthenticated)
            {
                currentUser = await _userManager.GetUserAsync(User);
            }

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
            var story = await _context.Stories
                .Include(s => s.Author)
                .FirstOrDefaultAsync(s => s.Id == storyId);

            if (story == null)
                return NotFound("Truyện không tồn tại.");

            var ratings = await _context.Ratings
                .Where(r => r.StoryId == storyId)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ApplicationUser currentUser = null;
            if (User.Identity.IsAuthenticated)
            {
                currentUser = await _userManager.GetUserAsync(User);
            }

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
                // Tính số chương
                chapterCounts[story.Id] = story.Chapters?.Count ?? 0;
            }

            ViewBag.ReadingProgresses = readingProgresses;
            ViewBag.ViewCounts = viewCounts;
            ViewBag.Ratings = ratings;
            ViewBag.ChapterCounts = chapterCounts;

            return View(libraryEntries);
        }
    }
}
