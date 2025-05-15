using InteractiveStoryWeb.Data;
using InteractiveStoryWeb.Models;
using InteractiveStoryWeb.Utils;
using InteractiveStoryWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace InteractiveStoryWeb.Controllers
{
    [Authorize]
    public class ChoiceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public ChoiceController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
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
                return Json(new { success = false, message = "Đoạn không tồn tại." });
            }

            if (!ModelState.IsValid)
            {
                // Lấy thông báo lỗi chi tiết từ ModelState
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                var errorMessage = errors.Any() ? string.Join("; ", errors) : "Vui lòng kiểm tra lại thông tin.";
                return Json(new { success = false, message = errorMessage });
            }

            // Kiểm tra đoạn hiện tại
            var currentSegment = await _context.ChapterSegments.FindAsync(model.ChapterSegmentId);
            if (currentSegment == null)
            {
                return Json(new { success = false, message = "Đoạn hiện tại không tồn tại." });
            }

            // Kiểm tra NextSegmentId
            var nextSegment = await _context.ChapterSegments
                .FirstOrDefaultAsync(s => s.Id == model.NextSegmentId && s.ChapterId == currentSegment.ChapterId);
            if (nextSegment == null)
            {
                return Json(new { success = false, message = "Đoạn tiếp theo không tồn tại hoặc không thuộc cùng chương." });
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

            var segments = await _context.ChapterSegments
                .Where(s => s.ChapterId == segment.ChapterId)
                .Select(s => new { id = s.Id, title = s.Title })
                .ToListAsync();

            return Json(new
            {
                success = true,
                message = "Lựa chọn đã được thêm thành công!",
                choice = new
                {
                    id = choice.Id,
                    choiceText = choice.ChoiceText,
                    nextSegmentId = choice.NextSegmentId,
                    nextSegmentTitle = nextSegment.Title,
                    chapterSegmentId = choice.ChapterSegmentId,
                    createdAt = choice.CreatedAt.ToString("o")
                },
                segments
            });
        }

        // Thêm action Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var choice = await _context.Choices
                .Include(c => c.ChapterSegment)
                    .ThenInclude(cs => cs.Chapter)
                        .ThenInclude(c => c.Story)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (choice == null)
            {
                return Json(new { success = false, message = "Lựa chọn không tồn tại." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (choice.ChapterSegment.Chapter.Story.AuthorId != user.Id)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa lựa chọn này." });
            }

            var segmentId = choice.ChapterSegmentId;
            _context.Choices.Remove(choice);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Lựa chọn đã được xóa thành công!",
                segmentId
            });
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

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ChooseJson(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            string currentUserId = currentUser?.Id;

            var choice = await _context.Choices
                .Include(c => c.NextSegment)
                    .ThenInclude(ns => ns.Chapter)
                        .ThenInclude(c => c.Story)
                            .ThenInclude(s => s.Author)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (choice == null || choice.NextSegment == null)
            {
                return Json(new { success = false, message = "Lựa chọn không hợp lệ hoặc không tồn tại." });
            }

            var nextSegment = choice.NextSegment;
            var chapter = nextSegment.Chapter;
            var story = chapter.Story;

            if (!chapter.IsPublic || !story.IsPublic || story.Author.IsBanned)
            {
                return Json(new { success = false, message = "Nội dung chưa được công khai hoặc tác giả đã bị cấm." });
            }

            // Kiểm tra trạng thái chặn
            if (currentUserId != null)
            {
                bool isStoryBlocked = await _context.Blocks
                    .AnyAsync(b => b.UserId == currentUserId && b.BlockedStoryId == story.Id);
                bool isAuthorBlocked = await _context.Blocks
                    .AnyAsync(b => b.UserId == currentUserId && b.BlockedUserId == story.AuthorId);
                bool isBlockedByAuthor = await _context.Blocks
                    .AnyAsync(b => b.UserId == story.AuthorId && b.BlockedUserId == currentUserId);

                if (isStoryBlocked || isAuthorBlocked || isBlockedByAuthor)
                {
                    return Json(new { success = false, message = "Không thể truy cập nội dung do trạng thái chặn." });
                }
            }

            // Lấy thông tin tùy chỉnh (nếu có)
            ReaderStoryCustomization customization = null;
            if (story.AllowCustomization)
            {
                string userId = currentUser?.Id ?? "anonymous";
                if (currentUser != null)
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
                    }
                }
            }

            // Định dạng nội dung với Markdown và tùy chỉnh
            var content = MarkdownFormatter.FormatContent(nextSegment.Content ?? string.Empty, customization);

            // Lấy danh sách lựa chọn cho segment tiếp theo
            var choices = await _context.Choices
                .Where(c => c.ChapterSegmentId == nextSegment.Id)
                .Include(c => c.NextSegment)
                    .ThenInclude(ns => ns.Chapter)
                .Where(c => c.NextSegment != null &&
                           c.NextSegment.Chapter != null &&
                           c.NextSegment.Chapter.StoryId == story.Id &&
                           c.NextSegment.Chapter.IsPublic)
                .Select(c => new
                {
                    id = c.Id,
                    choiceText = c.ChoiceText
                }).ToListAsync();

            return Json(new
            {
                success = true,
                data = new
                {
                    segmentId = nextSegment.Id,
                    chapterId = chapter.Id,
                    storyId = story.Id,
                    storyTitle = story.Title,
                    chapterTitle = chapter.Title,
                    segmentTitle = nextSegment.Title,
                    content = content,
                    imageUrl = nextSegment.ImageUrl,
                    imagePosition = nextSegment.ImagePosition.ToString(),
                    allowCustomization = story.AllowCustomization,
                    choices = choices,
                    createdAt = chapter.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    updatedAt = chapter.UpdatedAt?.ToString("dd/MM/yyyy HH:mm"),
                    authorId = story.Author?.Id,
                    authorUserName = story.Author?.UserName ?? "Unknown", // Đảm bảo không null
                    authorAvatarUrl = string.IsNullOrEmpty(story.Author?.AvatarUrl) ? "/images/AvatarNull.jpg" : story.Author.AvatarUrl,
                    isInLibrary = currentUser != null && await _context.Libraries.AnyAsync(l => l.UserId == currentUser.Id && l.StoryId == story.Id),
                    isAuthenticated = currentUser != null
                }
            });
        }
    }
}
