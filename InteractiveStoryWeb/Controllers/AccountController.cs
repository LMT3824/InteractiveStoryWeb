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

        public async Task<IActionResult> MyProfile()
        {
            var user = await _userManager.GetUserAsync(User);

            var myStories = await _context.Stories
                .Where(s => s.AuthorId == user.Id)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            ViewBag.User = user;
            return View(myStories);
        }
    }
}
