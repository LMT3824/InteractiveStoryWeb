using InteractiveStoryWeb.Data;
using InteractiveStoryWeb.Models;
using InteractiveStoryWeb.Services;
using InteractiveStoryWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InteractiveStoryWeb.Controllers
{
    [Authorize]
    public class ImportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileParserService _fileParserService;

        public ImportController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFileParserService fileParserService)
        {
            _context = context;
            _userManager = userManager;
            _fileParserService = fileParserService;
        }

        // GET: Import/Upload?storyId=1
        [HttpGet]
        public async Task<IActionResult> Upload(int storyId)
        {
            var story = await _context.Stories.FindAsync(storyId);
            if (story == null)
            {
                TempData["ErrorMessage"] = "Truyện không tồn tại.";
                return RedirectToAction("MyProfile", "Account");
            }

            var user = await _userManager.GetUserAsync(User);
            if (story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền import file cho truyện này.";
                return RedirectToAction("MyProfile", "Account");
            }

            ViewBag.StoryId = storyId;
            ViewBag.StoryTitle = story.Title;

            return View(new ImportFileViewModel { StoryId = storyId });
        }

        // POST: Import/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(ImportFileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn file để tải lên.";
                return RedirectToAction("Upload", new { storyId = model.StoryId });
            }

            var story = await _context.Stories.FindAsync(model.StoryId);
            if (story == null)
            {
                TempData["ErrorMessage"] = "Truyện không tồn tại.";
                return RedirectToAction("MyProfile", "Account");
            }

            var user = await _userManager.GetUserAsync(User);
            if (story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền import file cho truyện này.";
                return RedirectToAction("MyProfile", "Account");
            }

            // Kiểm tra định dạng file
            var extension = Path.GetExtension(model.File.FileName).ToLower();
            if (extension != ".docx" && extension != ".pdf")
            {
                TempData["ErrorMessage"] = "Chỉ hỗ trợ file .docx và .pdf";
                return RedirectToAction("Upload", new { storyId = model.StoryId });
            }

            // Kiểm tra kích thước file (max 10MB)
            if (model.File.Length > 10 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "File không được vượt quá 10MB.";
                return RedirectToAction("Upload", new { storyId = model.StoryId });
            }

            try
            {
                // Parse file
                var preview = await _fileParserService.ParseFileAsync(model.File, model.StoryId, story.Title);

                // Lưu preview vào TempData để hiển thị
                TempData["PreviewData"] = JsonSerializer.Serialize(preview);

                return RedirectToAction("Preview", new { storyId = model.StoryId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xử lý file: {ex.Message}";
                return RedirectToAction("Upload", new { storyId = model.StoryId });
            }
        }

        // GET: Import/Preview?storyId=1
        [HttpGet]
        public async Task<IActionResult> Preview(int storyId)
        {
            var previewDataJson = TempData["PreviewData"] as string;
            if (string.IsNullOrEmpty(previewDataJson))
            {
                TempData["ErrorMessage"] = "Không có dữ liệu preview. Vui lòng upload file lại.";
                return RedirectToAction("Upload", new { storyId });
            }

            var preview = JsonSerializer.Deserialize<ImportPreviewViewModel>(previewDataJson);

            // Lưu lại vào TempData để có thể sử dụng ở bước tiếp theo
            TempData.Keep("PreviewData");

            return View(preview);
        }

        // POST: Import/Confirm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int storyId, List<int> selectedChapters)
        {
            var previewDataJson = TempData["PreviewData"] as string;
            if (string.IsNullOrEmpty(previewDataJson))
            {
                TempData["ErrorMessage"] = "Phiên làm việc đã hết hạn. Vui lòng upload file lại.";
                return RedirectToAction("Upload", new { storyId });
            }

            var preview = JsonSerializer.Deserialize<ImportPreviewViewModel>(previewDataJson);

            var story = await _context.Stories.FindAsync(storyId);
            if (story == null)
            {
                TempData["ErrorMessage"] = "Truyện không tồn tại.";
                return RedirectToAction("MyProfile", "Account");
            }

            var user = await _userManager.GetUserAsync(User);
            if (story.AuthorId != user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền import file cho truyện này.";
                return RedirectToAction("MyProfile", "Account");
            }

            // Nếu không chọn chapter nào, lấy tất cả
            if (selectedChapters == null || !selectedChapters.Any())
            {
                selectedChapters = Enumerable.Range(0, preview.Chapters.Count).ToList();
            }

            try
            {
                int importedChapters = 0;
                int importedSegments = 0;

                foreach (var chapterIndex in selectedChapters)
                {
                    if (chapterIndex < 0 || chapterIndex >= preview.Chapters.Count)
                        continue;

                    var chapterPreview = preview.Chapters[chapterIndex];

                    // Tạo Chapter
                    var chapter = new Chapter
                    {
                        StoryId = storyId,
                        Title = chapterPreview.Title,
                        IsPublic = false, // Mặc định không công khai
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _context.Chapters.Add(chapter);
                    await _context.SaveChangesAsync();
                    importedChapters++;

                    // Tạo các Segment
                    foreach (var segmentPreview in chapterPreview.Segments)
                    {
                        var segment = new ChapterSegment
                        {
                            ChapterId = chapter.Id,
                            Title = segmentPreview.Title,
                            Content = segmentPreview.Content,
                            ImagePosition = ImagePosition.Bottom,
                            CreatedAt = DateTime.Now
                        };

                        _context.ChapterSegments.Add(segment);
                        importedSegments++;
                    }

                    await _context.SaveChangesAsync();

                    // KHÔNG tạo Choice tự động
                    // Người dùng sẽ tự tạo Choice sau khi import xong
                }

                TempData["SuccessMessage"] = $"Import thành công {importedChapters} chương và {importedSegments} đoạn! " +
                    $"Bạn có thể tạo các lựa chọn (choices) để liên kết các đoạn với nhau.";
                return RedirectToAction("Manage", "Chapter", new { storyId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi lưu dữ liệu: {ex.Message}";
                return RedirectToAction("Upload", new { storyId });
            }
        }

        // POST: Import/Cancel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel(int storyId)
        {
            TempData.Remove("PreviewData");
            return RedirectToAction("Manage", "Chapter", new { storyId });
        }
    }
}