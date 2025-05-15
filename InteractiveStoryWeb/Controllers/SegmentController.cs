using System.Text.RegularExpressions;
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
    public class SegmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;

        public SegmentController(ApplicationDbContext context, IWebHostEnvironment env, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Create(int chapterId)
        {
            var chapter = _context.Chapters.Find(chapterId);
            if (chapter == null) return NotFound();
            ViewBag.StoryId = chapter.StoryId;
            ViewBag.AllowCustomization = chapter.Story?.AllowCustomization ?? false;
            return View(new ChapterSegmentCreateViewModel { ChapterId = chapterId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(ChapterSegmentCreateViewModel model)
        {
            var chapter = await _context.Chapters
                .Include(c => c.Story)
                .FirstOrDefaultAsync(c => c.Id == model.ChapterId);

            if (chapter == null)
            {
                TempData["ErrorMessage"] = "Chương không tồn tại.";
                return RedirectToAction("Manage", "Chapter", new { storyId = ViewBag.StoryId });
            }

            var user = await _userManager.GetUserAsync(User);
            if (chapter.Story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thêm đoạn vào chương này.";
                return RedirectToAction("Manage", "Chapter", new { storyId = chapter.StoryId });
            }

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

            var segment = new ChapterSegment
            {
                ChapterId = model.ChapterId,
                Title = model.Title,
                Content = model.Content,
                ImageUrl = imagePath,
                ImagePosition = model.ImagePosition, // Lưu giá trị ImagePosition
                CreatedAt = DateTime.Now
            };

            _context.ChapterSegments.Add(segment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đoạn đã được tạo thành công!";
            return RedirectToAction("Manage", "Chapter", new { storyId = chapter.StoryId });
        }

        [AllowAnonymous]
        public async Task<IActionResult> InteractiveRead(int id)
        {
            try
            {
                Console.WriteLine($"InteractiveRead called with segmentId: {id}");

                // Kiểm tra id hợp lệ
                if (id <= 0)
                {
                    Console.WriteLine($"Invalid segmentId: {id}");
                    return RedirectToAction("Index", "Home");
                }

                var segment = await _context.ChapterSegments
                    .Include(s => s.Chapter)
                        .ThenInclude(c => c.Story)
                            .ThenInclude(s => s.Author)
                    .Include(s => s.Choices)
                        .ThenInclude(c => c.NextSegment)
                            .ThenInclude(ns => ns.Chapter)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (segment == null)
                {
                    Console.WriteLine($"Segment not found for ID: {id}");
                    return NotFound("Đoạn không tồn tại.");
                }

                Console.WriteLine($"Segment loaded: ID = {segment.Id}, ChapterId = {segment.ChapterId}");

                var chapter = segment.Chapter;
                var story = chapter.Story;

                if (chapter == null || !chapter.IsPublic || story == null || !story.IsPublic)
                {
                    Console.WriteLine($"Content not public for segmentId: {id}, chapterId: {chapter?.Id}, storyId: {story?.Id}");
                    return NotFound("Nội dung chưa được công khai.");
                }

                Console.WriteLine($"Chapter loaded: ID = {chapter.Id}, StoryId = {story.Id}, IsPublic = {chapter.IsPublic}");

                // Log số lượng Choices trước khi lọc
                Console.WriteLine($"Raw Choices count for segment {segment.Id}: {segment.Choices?.Count ?? 0}");
                if (segment.Choices != null)
                {
                    foreach (var choice in segment.Choices)
                    {
                        Console.WriteLine($"Raw Choice ID: {choice.Id}, NextSegmentId: {choice.NextSegmentId}, NextSegment: {(choice.NextSegment != null ? $"ID = {choice.NextSegment.Id}" : "null")}");
                    }
                }

                // Lọc các lựa chọn
                segment.Choices = segment.Choices
                    .Where(c => c.NextSegment != null &&
                                c.NextSegment.Chapter.StoryId == story.Id &&
                                c.NextSegment.Chapter.IsPublic)
                    .ToList();
                Console.WriteLine($"Choices for segment {segment.Id} after filtering: {segment.Choices.Count}");
                foreach (var choice in segment.Choices)
                {
                    Console.WriteLine($"Filtered Choice ID: {choice.Id}, NextSegmentId: {choice.NextSegmentId}, NextSegment ChapterId: {choice.NextSegment.ChapterId}, NextSegment Chapter IsPublic: {choice.NextSegment.Chapter.IsPublic}");
                }

                // Tăng lượt xem chapter chỉ khi vào đoạn đầu tiên
                var firstSegment = await _context.ChapterSegments
                    .Where(s => s.ChapterId == chapter.Id && s.Chapter.IsPublic)
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync();
                if (segment.Id == firstSegment?.Id)
                {
                    chapter.ViewCount++;
                    await _context.SaveChangesAsync();
                }

                // Lưu tiến trình đọc nếu người dùng đã đăng nhập
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var progress = await _context.ReadingProgresses
                        .FirstOrDefaultAsync(rp => rp.UserId == user.Id && rp.StoryId == story.Id);

                    if (progress == null)
                    {
                        progress = new ReadingProgress
                        {
                            UserId = user.Id,
                            StoryId = story.Id,
                            ChapterSegmentId = segment.Id,
                            LastReadAt = DateTime.Now
                        };
                        _context.ReadingProgresses.Add(progress);
                    }
                    else
                    {
                        progress.ChapterSegmentId = segment.Id;
                        progress.LastReadAt = DateTime.Now;
                    }
                    await _context.SaveChangesAsync();
                }

                // Kiểm tra xem truyện đã có trong thư viện chưa
                bool isInLibrary = user != null && await _context.Libraries.AnyAsync(l => l.UserId == user.Id && l.StoryId == story.Id);
                ViewBag.IsInLibrary = isInLibrary;

                // Thêm trạng thái đăng nhập vào ViewBag
                ViewBag.IsAuthenticated = user != null;

                // Kiểm tra tùy chỉnh nếu truyện có AllowCustomization = true
                ReaderStoryCustomization customization = null;
                if (story.AllowCustomization)
                {
                    string userId = user?.Id ?? "anonymous";
                    if (user != null)
                    {
                        customization = await _context.ReaderStoryCustomizations
                            .FirstOrDefaultAsync(rsc => rsc.StoryId == story.Id && rsc.UserId == userId);
                    }
                    else
                    {
                        var sessionKey = $"Customization_{story.Id}";
                        var sessionData = HttpContext.Session.GetString(sessionKey);
                        if (!string.IsNullOrEmpty(sessionData))
                        {
                            customization = JsonSerializer.Deserialize<ReaderStoryCustomization>(sessionData);
                            Console.WriteLine($"Retrieved customization from Session for storyId {story.Id}: {sessionData}");
                        }
                        else
                        {
                            Console.WriteLine($"No customization found in Session for storyId {story.Id}");
                        }
                    }
                }

                // Chuyển đổi nội dung thành HTML với Markdown và tùy chỉnh
                segment.Content = MarkdownFormatter.FormatContent(segment.Content ?? string.Empty, customization);

                ViewBag.StoryId = story.Id;
                ViewBag.CurrentChapterId = chapter.Id;
                ViewBag.Customization = customization;

                var hasNextChapter = await _context.Chapters
                    .AnyAsync(c => c.StoryId == story.Id && c.IsPublic && c.CreatedAt > chapter.CreatedAt);

                var chapters = await _context.Chapters
                    .Where(c => c.StoryId == story.Id && c.IsPublic)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();

                var currentIndex = chapters.FindIndex(c => c.Id == chapter.Id);
                ViewBag.HasPreviousChapter = currentIndex > 0;
                ViewBag.HasNextChapter = currentIndex < chapters.Count - 1;

                return View(segment);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in InteractiveRead: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, "Có lỗi xảy ra khi tải đoạn truyện.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> PrevChapter(int currentSegmentId)
        {
            var currentSegment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                    .ThenInclude(c => c.Story)
                        .ThenInclude(s => s.Chapters)
                .FirstOrDefaultAsync(s => s.Id == currentSegmentId);

            if (currentSegment == null || currentSegment.Chapter == null || currentSegment.Chapter.Story == null)
                return NotFound();

            var story = currentSegment.Chapter.Story;
            var currentChapter = currentSegment.Chapter;

            // Sắp xếp các chương theo CreatedAt và lọc chương công khai
            var chapters = await _context.Chapters
                .Where(c => c.StoryId == story.Id && c.IsPublic)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            var currentIndex = chapters.FindIndex(c => c.Id == currentChapter.Id);

            if (currentIndex <= 0) // Không có chương trước
            {
                TempData["ErrorMessage"] = "Không có chương trước để chuyển.";
                return RedirectToAction("InteractiveRead", new { id = currentSegmentId });
            }

            var prevChapter = chapters[currentIndex - 1];

            // Tìm segment đầu tiên của chương trước
            var firstSegment = await _context.ChapterSegments
                .Where(s => s.ChapterId == prevChapter.Id)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (firstSegment == null)
            {
                TempData["ErrorMessage"] = "Chương trước chưa có nội dung.";
                return RedirectToAction("InteractiveRead", new { id = currentSegmentId });
            }

            return RedirectToAction("InteractiveRead", new { id = firstSegment.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> NextChapter(int currentSegmentId)
        {
            var currentSegment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                    .ThenInclude(c => c.Story)
                .FirstOrDefaultAsync(s => s.Id == currentSegmentId);

            if (currentSegment == null || currentSegment.Chapter == null || currentSegment.Chapter.Story == null)
                return NotFound();

            var storyId = currentSegment.Chapter.StoryId;
            var currentChapter = currentSegment.Chapter;

            var nextChapter = await _context.Chapters
                .Where(c => c.StoryId == storyId && c.IsPublic && c.CreatedAt > currentChapter.CreatedAt)
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (nextChapter == null)
            {
                TempData["Message"] = "Không có chương tiếp theo.";
                return RedirectToAction("InteractiveRead", new { id = currentSegmentId });
            }

            var firstSegment = await _context.ChapterSegments
                .Where(s => s.ChapterId == nextChapter.Id)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (firstSegment == null)
            {
                TempData["Message"] = "Chương tiếp theo chưa có nội dung.";
                return RedirectToAction("InteractiveRead", new { id = currentSegmentId });
            }

            return RedirectToAction("InteractiveRead", new { id = firstSegment.Id });
        }

        [HttpGet]
        public async Task<IActionResult> GetSegmentJson(int id)
        {
            var segment = await _context.ChapterSegments
                .Include(s => s.Choices)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (segment == null)
                return NotFound();

            return Json(new
            {
                id = segment.Id,
                content = segment.Content,
                imageUrl = segment.ImageUrl,
                choices = segment.Choices.Select(c => new
                {
                    text = c.ChoiceText,
                    nextId = c.NextSegmentId
                })
            });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> PrevChapterJson(int currentChapterId)
        {
            Console.WriteLine($"PrevChapterJson called with currentChapterId: {currentChapterId}");

            var currentChapter = await _context.Chapters
                .Include(c => c.Story)
                    .ThenInclude(s => s.Author)
                .FirstOrDefaultAsync(c => c.Id == currentChapterId);

            if (currentChapter == null || currentChapter.Story == null)
            {
                Console.WriteLine($"Current chapter not found for ID: {currentChapterId}");
                return Json(new { success = false, message = "Chương không tồn tại." });
            }

            var storyId = currentChapter.StoryId;

            var prevChapter = await _context.Chapters
                .Where(c => c.StoryId == storyId && c.IsPublic && c.CreatedAt < currentChapter.CreatedAt)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (prevChapter == null)
            {
                return Json(new { success = false, message = "Không có chương trước để chuyển." });
            }

            var firstSegment = await _context.ChapterSegments
                .Include(s => s.Choices)
                    .ThenInclude(c => c.NextSegment)
                        .ThenInclude(ns => ns.Chapter)
                            .ThenInclude(c => c.Story)
                                .ThenInclude(s => s.Author)
                .Where(s => s.ChapterId == prevChapter.Id)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (firstSegment == null)
            {
                return Json(new { success = false, message = "Chương trước chưa có nội dung." });
            }

            // Tăng ViewCount nếu đây là đoạn đầu tiên của chương
            var firstSegmentOfChapter = await _context.ChapterSegments
                .Where(s => s.ChapterId == prevChapter.Id && s.Chapter.IsPublic)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();
            if (firstSegment.Id == firstSegmentOfChapter?.Id)
            {
                prevChapter.ViewCount++;
                await _context.SaveChangesAsync();
            }

            // Lấy thông tin người dùng một lần duy nhất
            var user = await _userManager.GetUserAsync(User);

            // Lấy thông tin tùy chỉnh nếu có
            ReaderStoryCustomization customization = null;
            if (currentChapter.Story.AllowCustomization)
            {
                string userId = user?.Id ?? "anonymous";

                if (user != null)
                {
                    customization = await _context.ReaderStoryCustomizations
                        .FirstOrDefaultAsync(rsc => rsc.StoryId == storyId && rsc.UserId == userId);
                }
                else
                {
                    var sessionKey = $"Customization_{storyId}";
                    var sessionData = HttpContext.Session.GetString(sessionKey);
                    if (!string.IsNullOrEmpty(sessionData))
                    {
                        customization = JsonSerializer.Deserialize<ReaderStoryCustomization>(sessionData);
                        Console.WriteLine($"Retrieved customization from Session for storyId {storyId}: {sessionData}");
                    }
                    else
                    {
                        Console.WriteLine($"No customization found in Session for storyId {storyId}");
                    }
                }
            }

            // Định dạng nội dung với Markdown và tùy chỉnh
            var content = MarkdownFormatter.FormatContent(firstSegment.Content ?? string.Empty, customization);

            // Xác định chương trước và chương sau
            var chapters = await _context.Chapters
                .Where(c => c.StoryId == storyId && c.IsPublic)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
            var currentIndex = chapters.FindIndex(c => c.Id == prevChapter.Id);
            var hasPreviousChapter = currentIndex > 0;
            var previousChapter = hasPreviousChapter ? chapters[currentIndex - 1] : null;
            var hasNextChapter = currentIndex >= 0 && currentIndex < chapters.Count - 1;
            var nextChapter = hasNextChapter ? chapters[currentIndex + 1] : null;

            // Lấy segment đầu tiên của chương trước và chương sau
            int? previousSegmentId = null;
            if (previousChapter != null)
            {
                var firstSegmentOfPrevious = await _context.ChapterSegments
                    .Where(s => s.ChapterId == previousChapter.Id)
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync();
                previousSegmentId = firstSegmentOfPrevious?.Id;
            }

            int? nextSegmentId = null;
            if (nextChapter != null)
            {
                var firstSegmentOfNext = await _context.ChapterSegments
                    .Where(s => s.ChapterId == nextChapter.Id)
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync();
                nextSegmentId = firstSegmentOfNext?.Id;
            }

            // Lấy danh sách các lựa chọn
            var choices = firstSegment.Choices
                .Where(c => c.NextSegment != null &&
                            c.NextSegment.Chapter.StoryId == storyId &&
                            c.NextSegment.Chapter.IsPublic)
                .Select(c => new
                {
                    id = c.Id,
                    choiceText = c.ChoiceText
                }).ToList();

            // Kiểm tra xem truyện đã có trong thư viện chưa
            var isInLibrary = user != null && await _context.Libraries.AnyAsync(l => l.UserId == user.Id && l.StoryId == storyId);

            // Lấy thông tin tác giả
            var author = currentChapter.Story.Author;
            var authorAvatarUrl = string.IsNullOrEmpty(author?.AvatarUrl) ? "/images/AvatarNull.jpg" : author.AvatarUrl;

            // Trả về dữ liệu JSON
            return Json(new
            {
                success = true,
                data = new
                {
                    segmentId = firstSegment.Id,
                    chapterId = prevChapter.Id,
                    storyId = storyId,
                    storyTitle = currentChapter.Story.Title,
                    chapterTitle = prevChapter.Title,
                    segmentTitle = firstSegment.Title,
                    content = content,
                    imageUrl = firstSegment.ImageUrl,
                    imagePosition = firstSegment.ImagePosition.ToString(),
                    allowCustomization = currentChapter.Story.AllowCustomization,
                    choices = choices,
                    hasNextChapter = hasNextChapter,
                    hasPreviousChapter = hasPreviousChapter,
                    previousSegmentId = previousSegmentId,
                    nextSegmentId = nextSegmentId,
                    createdAt = prevChapter.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    updatedAt = prevChapter.UpdatedAt?.ToString("dd/MM/yyyy HH:mm"),
                    authorId = author?.Id,
                    authorUserName = author?.UserName,
                    authorAvatarUrl = authorAvatarUrl,
                    isInLibrary = isInLibrary,
                    isAuthenticated = user != null
                }
            });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> NextChapterJson(int currentChapterId)
        {
            Console.WriteLine($"NextChapterJson called with currentChapterId: {currentChapterId}");

            var currentChapter = await _context.Chapters
                .Include(c => c.Story)
                    .ThenInclude(s => s.Author)
                .FirstOrDefaultAsync(c => c.Id == currentChapterId);

            if (currentChapter == null || currentChapter.Story == null)
            {
                Console.WriteLine($"Current chapter not found for ID: {currentChapterId}");
                return Json(new { success = false, message = "Chương không tồn tại." });
            }

            var storyId = currentChapter.StoryId;

            var nextChapter = await _context.Chapters
                .Where(c => c.StoryId == storyId && c.IsPublic && c.CreatedAt > currentChapter.CreatedAt)
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (nextChapter == null)
            {
                return Json(new { success = false, message = "Không có chương sau để chuyển." });
            }

            var firstSegment = await _context.ChapterSegments
                .Include(s => s.Choices)
                    .ThenInclude(c => c.NextSegment)
                        .ThenInclude(ns => ns.Chapter)
                            .ThenInclude(c => c.Story)
                                .ThenInclude(s => s.Author) // Thêm Include cho Author
                .Where(s => s.ChapterId == nextChapter.Id)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();

            if (firstSegment == null)
            {
                return Json(new { success = false, message = "Chương tiếp theo chưa có nội dung." });
            }

            // Tăng ViewCount nếu đây là đoạn đầu tiên của chương
            var firstSegmentOfChapter = await _context.ChapterSegments
                .Where(s => s.ChapterId == nextChapter.Id && s.Chapter.IsPublic)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync();
            if (firstSegment.Id == firstSegmentOfChapter?.Id)
            {
                nextChapter.ViewCount++;
                await _context.SaveChangesAsync();
            }

            // Lấy thông tin người dùng một lần duy nhất
            var user = await _userManager.GetUserAsync(User);

            // Lấy thông tin tùy chỉnh nếu có
            ReaderStoryCustomization customization = null;
            if (currentChapter.Story.AllowCustomization)
            {
                string userId = user?.Id ?? "anonymous";

                if (user != null)
                {
                    customization = await _context.ReaderStoryCustomizations
                        .FirstOrDefaultAsync(rsc => rsc.StoryId == storyId && rsc.UserId == userId);
                }
                else
                {
                    var sessionKey = $"Customization_{storyId}";
                    var sessionData = HttpContext.Session.GetString(sessionKey);
                    if (!string.IsNullOrEmpty(sessionData))
                    {
                        customization = JsonSerializer.Deserialize<ReaderStoryCustomization>(sessionData);
                        Console.WriteLine($"Retrieved customization from Session for storyId {storyId}: {sessionData}");
                    }
                    else
                    {
                        Console.WriteLine($"No customization found in Session for storyId {storyId}");
                    }
                }
            }

            // Định dạng nội dung với Markdown và tùy chỉnh
            var content = MarkdownFormatter.FormatContent(firstSegment.Content ?? string.Empty, customization);

            // Xác định chương trước và chương sau
            var chapters = await _context.Chapters
                .Where(c => c.StoryId == storyId && c.IsPublic)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
            var currentIndex = chapters.FindIndex(c => c.Id == nextChapter.Id);
            var hasPreviousChapter = currentIndex > 0;
            var previousChapter = hasPreviousChapter ? chapters[currentIndex - 1] : null;
            var hasNextChapter = currentIndex >= 0 && currentIndex < chapters.Count - 1;
            var nextChapterAfter = hasNextChapter ? chapters[currentIndex + 1] : null;

            // Lấy segment đầu tiên của chương trước và chương sau
            int? previousSegmentId = null;
            if (previousChapter != null)
            {
                var firstSegmentOfPrevious = await _context.ChapterSegments
                    .Where(s => s.ChapterId == previousChapter.Id)
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync();
                previousSegmentId = firstSegmentOfPrevious?.Id;
            }

            int? nextSegmentId = null;
            if (nextChapterAfter != null)
            {
                var firstSegmentOfNext = await _context.ChapterSegments
                    .Where(s => s.ChapterId == nextChapterAfter.Id)
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync();
                nextSegmentId = firstSegmentOfNext?.Id;
            }

            // Lấy danh sách các lựa chọn
            var choices = firstSegment.Choices
                .Where(c => c.NextSegment != null &&
                            c.NextSegment.Chapter.StoryId == storyId &&
                            c.NextSegment.Chapter.IsPublic)
                .Select(c => new
                {
                    id = c.Id,
                    choiceText = c.ChoiceText
                }).ToList();

            // Kiểm tra xem truyện đã có trong thư viện chưa
            var isInLibrary = user != null && await _context.Libraries.AnyAsync(l => l.UserId == user.Id && l.StoryId == storyId);

            // Lấy thông tin tác giả
            var author = currentChapter.Story.Author;
            var authorAvatarUrl = string.IsNullOrEmpty(author?.AvatarUrl) ? "/images/AvatarNull.jpg" : author.AvatarUrl;

            // Trả về dữ liệu JSON
            return Json(new
            {
                success = true,
                data = new
                {
                    segmentId = firstSegment.Id,
                    chapterId = nextChapter.Id,
                    storyId = storyId,
                    storyTitle = currentChapter.Story.Title,
                    chapterTitle = nextChapter.Title,
                    segmentTitle = firstSegment.Title,
                    content = content,
                    imageUrl = firstSegment.ImageUrl,
                    imagePosition = firstSegment.ImagePosition.ToString(),
                    allowCustomization = currentChapter.Story.AllowCustomization,
                    choices = choices,
                    hasNextChapter = hasNextChapter,
                    hasPreviousChapter = hasPreviousChapter,
                    previousSegmentId = previousSegmentId,
                    nextSegmentId = nextSegmentId,
                    createdAt = nextChapter.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    updatedAt = nextChapter.UpdatedAt?.ToString("dd/MM/yyyy HH:mm"),
                    authorId = author?.Id,
                    authorUserName = author?.UserName,
                    authorAvatarUrl = authorAvatarUrl,
                    isInLibrary = isInLibrary,
                    isAuthenticated = user != null
                }
            });
        }

        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var segment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                .ThenInclude(c => c.Story)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (segment == null)
                return NotFound("Đoạn không tồn tại.");

            var user = await _userManager.GetUserAsync(User);
            if (segment.Chapter.Story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa đoạn này.";
                return RedirectToAction("Manage", "Chapter", new { storyId = segment.Chapter.StoryId });
            }

            var model = new ChapterSegmentEditViewModel
            {
                Id = segment.Id,
                ChapterId = segment.ChapterId,
                Title = segment.Title,
                Content = segment.Content,
                ImageUrl = segment.ImageUrl,
                ImagePosition = segment.ImagePosition,
                CreatedAt = segment.CreatedAt
            };

            ViewBag.StoryId = segment.Chapter.StoryId;
            ViewBag.AllowCustomization = segment.Chapter.Story.AllowCustomization;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(ChapterSegmentEditViewModel model)
        {
            var segment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                .ThenInclude(c => c.Story)
                .FirstOrDefaultAsync(s => s.Id == model.Id);

            if (segment == null)
                return NotFound("Đoạn không tồn tại.");

            var user = await _userManager.GetUserAsync(User);
            if (segment.Chapter.Story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa đoạn này.";
                return RedirectToAction("Manage", "Chapter", new { storyId = segment.Chapter.StoryId });
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin.";
                return View(model);
            }

            // Giữ giá trị ImageUrl hiện tại nếu không có ảnh mới
            string imagePath = model.ImageUrl;
            if (model.NewImage != null && model.NewImage.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads/segments");
                Directory.CreateDirectory(uploads);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.NewImage.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.NewImage.CopyToAsync(stream);
                }

                imagePath = "/uploads/segments/" + fileName;

                if (!string.IsNullOrEmpty(segment.ImageUrl))
                {
                    var oldImagePath = Path.Combine(_env.WebRootPath, segment.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }
            }

            segment.Title = model.Title;
            segment.Content = model.Content;
            segment.ImageUrl = imagePath;
            segment.ImagePosition = model.ImagePosition;
            segment.CreatedAt = model.CreatedAt;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đoạn đã được cập nhật thành công!";
            return RedirectToAction("Manage", "Chapter", new { storyId = segment.Chapter.StoryId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var segment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                    .ThenInclude(c => c.Story)
                .Include(s => s.Choices)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (segment == null)
            {
                return Json(new { success = false, message = "Đoạn không tồn tại." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (segment.Chapter.Story.AuthorId != user.Id)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa đoạn này." });
            }

            // Kiểm tra xem đoạn có được liên kết bởi các lựa chọn khác không (NextSegmentId)
            var linkedChoices = await _context.Choices
                .Where(c => c.NextSegmentId == segment.Id)
                .ToListAsync();

            if (linkedChoices.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Không thể xóa đoạn này vì nó được liên kết bởi các lựa chọn khác.",
                    storyId = segment.Chapter.StoryId
                });
            }

            // Đặt ChapterSegmentId trong ReadingProgress thành null trước khi xóa
            var relatedProgresses = await _context.ReadingProgresses
                .Where(rp => rp.ChapterSegmentId == segment.Id)
                .ToListAsync();

            foreach (var progress in relatedProgresses)
            {
                progress.ChapterSegmentId = null;
            }

            _context.ChapterSegments.Remove(segment);
            await _context.SaveChangesAsync();

            // Sau khi xóa đoạn, kiểm tra xem chương có còn đoạn nào không
            var chapter = await _context.Chapters
                .Include(c => c.Segments)
                .Include(c => c.Story)
                .FirstOrDefaultAsync(c => c.Id == segment.ChapterId);

            if (chapter != null && chapter.Segments != null && !chapter.Segments.Any())
            {
                // Nếu chương không còn đoạn nào, set chương thành không công khai
                chapter.IsPublic = false;

                // Kiểm tra xem Story có còn chương công khai nào có đoạn không
                var story = chapter.Story;
                var hasValidPublicChapter = await _context.Chapters
                    .Include(c => c.Segments)
                    .AnyAsync(c => c.StoryId == story.Id && c.IsPublic && c.Segments.Any());

                if (!hasValidPublicChapter && story.IsPublic)
                {
                    // Nếu không còn chương công khai nào có đoạn, đặt Story thành không công khai
                    story.IsPublic = false;
                }

                await _context.SaveChangesAsync();
            }

            return Json(new
            {
                success = true,
                message = "Đoạn đã được xóa thành công!",
                chapterId = segment.ChapterId
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Preview(int chapterId, string content, ImagePosition imagePosition, IFormFile image, string imageUrl)
        {
            var chapter = await _context.Chapters
                .Include(c => c.Story)
                .FirstOrDefaultAsync(c => c.Id == chapterId);

            if (chapter == null)
            {
                return Json(new { success = false, message = "Chương không tồn tại." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (chapter.Story.AuthorId != user.Id)
            {
                return Json(new { success = false, message = "Bạn không có quyền xem trước đoạn này." });
            }

            ReaderStoryCustomization customization = null;
            if (chapter.Story.AllowCustomization)
            {
               
                     customization = new ReaderStoryCustomization
                    {
                        Name = "NgườiĐọc",
                        FirstPersonPronoun = "Tôi",
                        SecondPersonPronoun = "Bạn"
                    };
            }

            var previewContent = MarkdownFormatter.FormatContent(content, customization);
            var html = new System.Text.StringBuilder();

            string finalImageUrl = imageUrl; // Sử dụng imageUrl nếu không có ảnh mới
            if (image != null && image.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads/temp");
                Directory.CreateDirectory(uploads);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                finalImageUrl = "/uploads/temp/" + fileName;
            }

            if (!string.IsNullOrEmpty(finalImageUrl) && imagePosition == ImagePosition.Top)
            {
                html.AppendLine("<div class=\"text-center mb-3\">");
                html.AppendLine($"<img src=\"{finalImageUrl}\" class=\"img-fluid rounded shadow segment-image\" alt=\"Ảnh minh họa\" />");
                html.AppendLine("</div>");
            }

            html.AppendLine(previewContent);

            if (!string.IsNullOrEmpty(finalImageUrl) && imagePosition == ImagePosition.Bottom)
            {
                html.AppendLine("<div class=\"text-center mb-3\">");
                html.AppendLine($"<img src=\"{finalImageUrl}\" class=\"img-fluid rounded shadow segment-image\" alt=\"Ảnh minh họa\" />");
                html.AppendLine("</div>");
            }

            return Json(new { success = true, previewContent = html.ToString() });
        }
    }
}
