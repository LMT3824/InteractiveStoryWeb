using InteractiveStoryWeb.Data;
using InteractiveStoryWeb.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeViewModel
        {
            TopViewed = await _context.Stories
            .Where(s => s.IsPublic)
            .Include(s => s.Chapters)
            .OrderByDescending(s => s.Chapters.Sum(ch => ch.ViewCount))
            .Take(4)
            .ToListAsync(),
            NewStories = await _context.Stories
            .Where(s => s.IsPublic)
            .Include(s => s.Chapters)
            .OrderByDescending(s => s.CreatedAt)
            .Take(4)
            .ToListAsync(),
            CompletedStories = await _context.Stories
            .Where(s => s.IsPublic && s.IsCompleted)
            .Include(s => s.Chapters)
            .OrderByDescending(s => s.CreatedAt)
            .Take(4)
            .ToListAsync()
        };

        // Tính tổng ViewCount cho từng Story
        var viewCounts = new Dictionary<int, int>();
        foreach (var story in model.TopViewed.Concat(model.NewStories).Concat(model.CompletedStories).Distinct())
        {
            viewCounts[story.Id] = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;
        }
        ViewBag.ViewCounts = viewCounts;

        return View(model);
    }
}
