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
            story.IsCompleted = model.IsCompleted;
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

            // Lấy thông tin người dùng hiện tại
            ApplicationUser currentUser = null;
            if (User.Identity.IsAuthenticated)
            {
                currentUser = await _userManager.GetUserAsync(User);
            }

            ViewBag.FirstSegmentId = firstSegment?.Id;
            ViewBag.TotalViewCount = totalViewCount;
            ViewBag.Comments = comments;
            ViewBag.Ratings = ratings;
            ViewBag.AverageRating = ratings.Any() ? ratings.Average(r => r.RatingValue) : 0;
            ViewBag.CurrentUser = currentUser; // Truyền người dùng hiện tại vào ViewBag
            if (!string.IsNullOrEmpty(errorMessage))
            {
                TempData["ErrorMessage"] = errorMessage;
            }
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var story = await _context.Stories
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.Segments) // Bao gồm Segments để lấy ChapterSegmentId
                .FirstOrDefaultAsync(s => s.Id == id);

            if (story == null)
            {
                TempData["ErrorMessage"] = "Truyện không tồn tại.";
                return RedirectToAction("Index");
            }

            var user = await _userManager.GetUserAsync(User);
            if (story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xóa truyện này.";
                return RedirectToAction("Index");
            }

            // Đặt ChapterSegmentId trong ReadingProgress thành null trước khi xóa
            var segmentIds = story.Chapters
                .SelectMany(c => c.Segments)
                .Select(s => s.Id)
                .ToList();

            var relatedProgresses = await _context.ReadingProgresses
                .Where(rp => segmentIds.Contains(rp.ChapterSegmentId.Value))
                .ToListAsync();

            foreach (var progress in relatedProgresses)
            {
                progress.ChapterSegmentId = null;
            }

            _context.Stories.Remove(story);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Truyện đã được xóa thành công!";
            return RedirectToAction("MyProfile", "Account");
        }

        // Actions for Comments
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

            if (string.IsNullOrEmpty(content))
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
                avatarUrl = string.IsNullOrEmpty(user.AvatarUrl) ? "/images/AvatarNotFound.png" : user.AvatarUrl // Thêm avatarUrl vào phản hồi
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

            if (string.IsNullOrEmpty(content))
            {
                return Json(new { success = false, message = "Nội dung bình luận không được để trống." });
            }

            comment.Content = content;
            await _context.SaveChangesAsync();

            return Json(new { success = true, content = comment.Content });
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

            return Json(new { success = true });
        }

        // Actions for Ratings
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
                RatingValue = ratingValue,
                CreatedAt = DateTime.Now
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
                userName = user.UserName,
                ratingValue = rating.RatingValue,
                createdAt = rating.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                averageRating = averageRating
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
    }
}
