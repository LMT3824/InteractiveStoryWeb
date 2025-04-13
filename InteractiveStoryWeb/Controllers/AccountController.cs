using InteractiveStoryWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InteractiveStoryWeb.Models;

namespace InteractiveStoryWeb.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [Authorize]
        public async Task<IActionResult> MyProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            var stories = await _context.Stories
                .Where(s => s.AuthorId == user.Id)
                .Include(s => s.Chapters)
                .ToListAsync();

            // Tính tổng ViewCount cho từng Story
            var viewCounts = new Dictionary<int, int>();
            foreach (var story in stories)
            {
                viewCounts[story.Id] = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;
            }
            ViewBag.ViewCounts = viewCounts;
            ViewBag.User = user;

            return View(stories);
        }
    }
}
