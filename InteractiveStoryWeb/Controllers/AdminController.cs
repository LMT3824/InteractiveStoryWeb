using InteractiveStoryWeb.Data;
using InteractiveStoryWeb.Hubs;
using InteractiveStoryWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace InteractiveStoryWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger _logger;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IHubContext<NotificationHub> hubContext, ILogger<AccountController> logger)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult SendNotification()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendNotification(string title, string content)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                ModelState.AddModelError("title", "Tiêu đề thông báo không được để trống.");
                return View();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                ModelState.AddModelError("content", "Nội dung thông báo không được để trống.");
                return View();
            }

            try
            {
                // Tạo thông báo toàn server
                var notification = new Notification
                {
                    Title = title,
                    Content = content,
                    CreatedAt = DateTime.Now,
                    UserId = null // Thông báo toàn server
                                  // Bỏ thuộc tính IsRead vì không dùng nữa
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                // Lấy tất cả người dùng
                var users = await _context.Users.ToListAsync();
                foreach (var user in users)
                {
                    var userNotificationRead = new UserNotificationRead
                    {
                        UserId = user.Id,
                        NotificationId = notification.Id,
                        IsRead = false
                    };
                    _context.UserNotificationReads.Add(userNotificationRead);
                }
                await _context.SaveChangesAsync();

                // Broadcast thông báo qua SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    title = notification.Title,
                    content = notification.Content,
                    createdAt = notification.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                });

                TempData["SuccessMessage"] = "Thông báo đã được gửi thành công!";
                return RedirectToAction("SendNotification");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                return View();
            }
        }

        // Hiển thị danh sách người dùng và truyện ẩn
        public async Task<IActionResult> ManageAccountStory()
        {
            var users = await _userManager.Users
                .Where(u => u.IsBanned)
                .ToListAsync();

            var hiddenStories = await _context.Stories
                .Where(s => s.IsHidden)
                .Include(s => s.Author)
                .ToListAsync();

            ViewBag.HiddenStories = hiddenStories;
            return View(users);
        }  

        // Chặn tài khoản
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BanUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Người dùng không tồn tại.";
                return RedirectToAction("ManageUsers");
            }

            user.IsBanned = true;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                // Gửi thông báo đến người dùng bị chặn
                var notification = new Notification
                {
                    Title = "Tài khoản bị chặn",
                    Content = "Tài khoản của bạn đã bị chặn bởi quản trị viên.",
                    CreatedAt = DateTime.Now,
                    UserId = user.Id
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                // Gửi qua SignalR nếu người dùng đang online
                await _hubContext.Clients.User(user.Id).SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    title = notification.Title,
                    content = notification.Content,
                    createdAt = notification.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                });

                TempData["SuccessMessage"] = $"Tài khoản {user.UserName} đã bị chặn.";
            }
            else
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi chặn tài khoản.";
            }

            return RedirectToAction("ManageAccountStory");
        }

        // Mở chặn tài khoản
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnbanUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Người dùng không tồn tại.";
                return RedirectToAction("ManageUsers");
            }

            user.IsBanned = false;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                // Gửi thông báo đến người dùng được mở chặn
                var notification = new Notification
                {
                    Title = "Tài khoản được mở chặn",
                    Content = "Tài khoản của bạn đã được mở chặn bởi quản trị viên.",
                    CreatedAt = DateTime.Now,
                    UserId = user.Id
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                // Gửi qua SignalR nếu người dùng đang online
                await _hubContext.Clients.User(user.Id).SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    title = notification.Title,
                    content = notification.Content,
                    createdAt = notification.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                });

                TempData["SuccessMessage"] = $"Tài khoản {user.UserName} đã được mở chặn.";
            }
            else
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi mở chặn tài khoản.";
            }

            return RedirectToAction("ManageAccountStory");
        }

        // Hiển thị danh sách báo cáo
        public async Task<IActionResult> ManageReports()
        {
            var reports = await _context.Reports
                .Include(r => r.User)
                .Include(r => r.Story)
                .Include(r => r.Comment)
                .Include(r => r.Author)
                .ToListAsync();
            return View(reports);
        }

        // Ẩn truyện
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HideStory(int storyId)
        {
            try
            {
                var story = await _context.Stories
                    .FirstOrDefaultAsync(s => s.Id == storyId);

                if (story == null)
                {
                    return Json(new { success = false, message = "Truyện không tồn tại." });
                }

                story.IsHidden = true; // Đánh dấu truyện là ẩn
                await _context.SaveChangesAsync();

                // Gửi thông báo đến tác giả
                var notification = new Notification
                {
                    Title = "Truyện bị ẩn",
                    Content = $"Truyện '{story.Title}' của bạn đã bị ẩn do vi phạm. Vui lòng liên hệ admin để được hỗ trợ.",
                    CreatedAt = DateTime.Now,
                    UserId = story.AuthorId
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                // Gửi qua SignalR nếu tác giả đang online
                await _hubContext.Clients.User(story.AuthorId).SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    title = notification.Title,
                    content = notification.Content,
                    createdAt = notification.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                });

                return Json(new { success = true, message = "Truyện đã được ẩn thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Có lỗi xảy ra khi ẩn truyện: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnhideStory(int storyId)
        {
            try
            {
                var story = await _context.Stories
                    .FirstOrDefaultAsync(s => s.Id == storyId);

                if (story == null)
                {
                    _logger.LogWarning($"UnhideStory: Story with ID {storyId} not found.");
                    return Json(new { success = false, message = "Truyện không tồn tại." });
                }

                story.IsHidden = false;
                await _context.SaveChangesAsync();

                var notification = new Notification
                {
                    Title = "Truyện được hủy ẩn",
                    Content = $"Truyện '{story.Title}' của bạn đã được hủy ẩn bởi quản trị viên.",
                    CreatedAt = DateTime.Now,
                    UserId = story.AuthorId
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                await _hubContext.Clients.User(story.AuthorId).SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    title = notification.Title,
                    content = notification.Content,
                    createdAt = notification.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                });

                return Json(new { success = true, message = "Truyện đã được hủy ẩn thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unhiding story with ID {storyId}.");
                return Json(new { success = false, message = $"Có lỗi xảy ra khi hủy ẩn truyện: {ex.Message}" });
            }
        }

        // Bỏ qua báo cáo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DismissReport(int reportId)
        {
            var report = await _context.Reports
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
            {
                return Json(new { success = false, message = "Báo cáo không tồn tại." });
            }

            _context.Reports.Remove(report);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Báo cáo đã được bỏ qua thành công!" });
        }

        public async Task<IActionResult> ManageSupportTickets()
        {
            var supportTickets = await _context.SupportTickets
                .Include(st => st.User)
                .Include(st => st.SupportTicketResponses) // Tải các phản hồi
                    .ThenInclude(str => str.Admin) // Tải thông tin admin
                .OrderByDescending(st => st.CreatedAt)
                .ToListAsync();

            return View(supportTickets);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondToSupportTicket(int ticketId, string content)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return Json(new { success = false, message = "Admin không tồn tại." });
            }

            var ticket = await _context.SupportTickets
                .FirstOrDefaultAsync(st => st.Id == ticketId);
            if (ticket == null)
            {
                return Json(new { success = false, message = "Yêu cầu hỗ trợ không tồn tại." });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, message = "Nội dung phản hồi không được để trống." });
            }

            if (content.Length > 1000)
            {
                return Json(new { success = false, message = "Nội dung phản hồi không được vượt quá 1000 ký tự." });
            }

            try
            {
                // Tạo phản hồi
                var response = new SupportTicketResponse
                {
                    SupportTicketId = ticketId,
                    AdminId = admin.Id,
                    Content = content,
                    CreatedAt = DateTime.Now
                };
                _context.SupportTicketResponses.Add(response);

                // Đánh dấu ticket là đã giải quyết
                ticket.IsResolved = true;
                _context.SupportTickets.Update(ticket);

                // Tạo thông báo
                var notification = new Notification
                {
                    Title = "Phản hồi yêu cầu hỗ trợ",
                    Content = $"Yêu cầu hỗ trợ của bạn đã được trả lời bởi admin. Kiểm tra tại danh sách yêu cầu hỗ trợ.",
                    CreatedAt = DateTime.Now,
                    UserId = ticket.UserId
                };
                _context.Notifications.Add(notification);

                // Lưu các thay đổi để tạo Notification và lấy NotificationId
                await _context.SaveChangesAsync();

                // Tạo UserNotificationRead với NotificationId hợp lệ
                var userNotificationRead = new UserNotificationRead
                {
                    UserId = ticket.UserId,
                    NotificationId = notification.Id, // Bây giờ notification.Id đã có giá trị
                    IsRead = false
                };
                _context.UserNotificationReads.Add(userNotificationRead);

                // Lưu lần nữa để thêm UserNotificationRead
                await _context.SaveChangesAsync();

                // Gửi qua SignalR nếu người dùng đang online
                await _hubContext.Clients.User(ticket.UserId).SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    title = notification.Title,
                    content = notification.Content,
                    createdAt = notification.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                });

                return Json(new { success = true, message = "Phản hồi đã được gửi thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi trả lời yêu cầu hỗ trợ.");
                return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
            }
        }
    }
}