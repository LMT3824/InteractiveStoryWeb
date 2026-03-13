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
    public class HighlightController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HighlightController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Lấy tất cả highlights của user cho segment hiện tại
        [HttpGet]
        public async Task<IActionResult> GetHighlights(int segmentId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập." });
            }

            var highlights = await _context.UserHighlights
                .Where(h => h.UserId == user.Id && h.ChapterSegmentId == segmentId)
                .OrderBy(h => h.StartOffset) // SẮP XẾP THEO OFFSET
                .Select(h => new
                {
                    id = h.Id,
                    highlightedText = h.HighlightedText,
                    contextBefore = h.ContextBefore,
                    contextAfter = h.ContextAfter,
                    startOffset = h.StartOffset,  // TRẢ VỀ OFFSET
                    endOffset = h.EndOffset,      // TRẢ VỀ OFFSET
                    color = h.Color,
                    note = h.Note,
                    createdAt = h.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return Json(new { success = true, highlights });
        }


        // Tạo highlight mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] HighlightViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập." });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, message = string.Join("; ", errors) });
            }

            var segment = await _context.ChapterSegments
                .Include(s => s.Chapter)
                    .ThenInclude(c => c.Story)
                .FirstOrDefaultAsync(s => s.Id == model.ChapterSegmentId);

            if (segment == null || !segment.Chapter.IsPublic || !segment.Chapter.Story.IsPublic)
            {
                return Json(new { success = false, message = "Đoạn truyện không tồn tại hoặc không công khai." });
            }

            var highlight = new UserHighlight
            {
                UserId = user.Id,
                ChapterSegmentId = model.ChapterSegmentId,
                HighlightedText = model.HighlightedText,
                ContextBefore = model.ContextBefore,
                ContextAfter = model.ContextAfter,
                StartOffset = model.StartOffset,  // LƯU OFFSET
                EndOffset = model.EndOffset,      // LƯU OFFSET
                Color = model.Color,
                Note = model.Note,
                CreatedAt = DateTime.Now
            };

            _context.UserHighlights.Add(highlight);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Đã lưu highlight thành công!",
                highlight = new
                {
                    id = highlight.Id,
                    highlightedText = highlight.HighlightedText,
                    contextBefore = highlight.ContextBefore,
                    contextAfter = highlight.ContextAfter,
                    startOffset = highlight.StartOffset,  // TRẢ VỀ OFFSET
                    endOffset = highlight.EndOffset,      // TRẢ VỀ OFFSET
                    color = highlight.Color,
                    note = highlight.Note,
                    createdAt = highlight.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                }
            });
        }

        // Cập nhật highlight (màu hoặc note)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromBody] HighlightViewModel model)
        {
            if (!model.Id.HasValue)
            {
                return Json(new { success = false, message = "ID highlight không hợp lệ." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập để sử dụng tính năng này." });
            }

            var highlight = await _context.UserHighlights
                .FirstOrDefaultAsync(h => h.Id == model.Id.Value && h.UserId == user.Id);

            if (highlight == null)
            {
                return Json(new { success = false, message = "Highlight không tồn tại hoặc bạn không có quyền chỉnh sửa." });
            }

            highlight.Color = model.Color;
            highlight.Note = model.Note;
            highlight.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Đã cập nhật highlight thành công!",
                highlight = new
                {
                    id = highlight.Id,
                    color = highlight.Color,
                    note = highlight.Note
                }
            });
        }

        // Xóa highlight
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập để sử dụng tính năng này." });
            }

            var highlight = await _context.UserHighlights
                .FirstOrDefaultAsync(h => h.Id == id && h.UserId == user.Id);

            if (highlight == null)
            {
                return Json(new { success = false, message = "Highlight không tồn tại hoặc bạn không có quyền xóa." });
            }

            _context.UserHighlights.Remove(highlight);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa highlight thành công!" });
        }
    }
}