using InteractiveStoryWeb.Data;
using InteractiveStoryWeb.Models;
using InteractiveStoryWeb.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        string currentUserId = currentUser?.Id;
        var sixMonthsAgo = DateTime.Now.AddMonths(-6);

        var popularStories = await _context.Stories
            .Where(s => s.IsPublic && !s.Author.IsBanned && !s.IsHidden
                && (currentUserId == null ||
                    (!_context.Blocks.Any(b => b.UserId == currentUserId && (b.BlockedUserId == s.AuthorId || b.BlockedStoryId == s.Id)) &&
                     !_context.Blocks.Any(b => b.UserId == s.AuthorId && b.BlockedUserId == currentUserId))))
            .Include(s => s.Author)
            .Include(s => s.Chapters)
            .Where(s => s.UpdatedAt >= sixMonthsAgo || s.Chapters.Any(c => c.CreatedAt >= sixMonthsAgo))
            .Select(s => new
            {
                Story = s,
                FollowerCount = _context.Follows.Count(f => f.FollowingId == s.AuthorId),
                AverageRating = _context.Ratings.Where(r => r.StoryId == s.Id).Average(r => (double?)r.RatingValue) ?? 0
            })
            .OrderByDescending(x => x.FollowerCount)
            .ThenByDescending(x => x.AverageRating)
            .ThenByDescending(x => x.Story.UpdatedAt ?? x.Story.CreatedAt)
            .Select(x => x.Story)
            .ToListAsync();

        var topRatedStoriesQuery = _context.Stories
            .Where(s => s.IsPublic && !s.Author.IsBanned && !s.IsHidden
                && (currentUserId == null ||
                    (!_context.Blocks.Any(b => b.UserId == currentUserId && (b.BlockedUserId == s.AuthorId || b.BlockedStoryId == s.Id)) &&
                     !_context.Blocks.Any(b => b.UserId == s.AuthorId && b.BlockedUserId == currentUserId))))
            .Include(s => s.Chapters);

        var topRatedStoriesList = await topRatedStoriesQuery.ToListAsync();
        Console.WriteLine($"Index - TopRated - Total Stories Before Sorting: {topRatedStoriesList.Count}");
        Console.WriteLine($"Index - TopRated - Titles Before Sorting: {string.Join(", ", topRatedStoriesList.Select(s => s.Title))}");

        var topRatedStories = topRatedStoriesList
            .Select(s => new
            {
                Story = s,
                AverageRating = _context.Ratings.Where(r => r.StoryId == s.Id).Average(r => (double?)r.RatingValue) ?? 0
            })
            .OrderByDescending(x => x.AverageRating)
            .ThenByDescending(x => x.Story.CreatedAt)
            .Select(x => x.Story)
            .ToList();

        Console.WriteLine($"Index - TopRated - Titles After Sorting: {string.Join(", ", topRatedStories.Select(s => s.Title))}");

        var newStories = await _context.Stories
            .Where(s => s.IsPublic && !s.Author.IsBanned && !s.IsHidden
                && (currentUserId == null ||
                    (!_context.Blocks.Any(b => b.UserId == currentUserId && (b.BlockedUserId == s.AuthorId || b.BlockedStoryId == s.Id)) &&
                     !_context.Blocks.Any(b => b.UserId == s.AuthorId && b.BlockedUserId == currentUserId))))
            .Include(s => s.Chapters)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var completedStories = await _context.Stories
            .Where(s => s.IsPublic && s.IsCompleted && !s.Author.IsBanned && !s.IsHidden
                && (currentUserId == null ||
                    (!_context.Blocks.Any(b => b.UserId == currentUserId && (b.BlockedUserId == s.AuthorId || b.BlockedStoryId == s.Id)) &&
                     !_context.Blocks.Any(b => b.UserId == s.AuthorId && b.BlockedUserId == currentUserId))))
            .Include(s => s.Chapters)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var model = new HomeViewModel
        {
            TopViewed = popularStories,
            NewStories = newStories,
            CompletedStories = completedStories,
            TopRatedStories = topRatedStories
        };

        var viewCounts = new Dictionary<int, int>();
        var ratings = new Dictionary<int, double>();
        var chapterCounts = new Dictionary<int, int>();

        var allStories = popularStories.Concat(topRatedStories).Concat(newStories).Concat(completedStories).Distinct().ToList();

        foreach (var story in allStories)
        {
            viewCounts[story.Id] = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;
            var storyRatings = await _context.Ratings
                .Where(r => r.StoryId == story.Id && !r.User.IsBanned
                    && (currentUserId == null ||
                        (!_context.Blocks.Any(b => b.UserId == currentUserId && b.BlockedUserId == r.UserId) &&
                         !_context.Blocks.Any(b => b.UserId == r.UserId && b.BlockedUserId == currentUserId))))
                .ToListAsync();
            ratings[story.Id] = storyRatings.Any() ? storyRatings.Average(r => r.RatingValue) : 0;
            chapterCounts[story.Id] = story.Chapters?.Count(ch => ch.IsPublic) ?? 0;
        }

        ViewBag.ViewCounts = viewCounts;
        ViewBag.Ratings = ratings;
        ViewBag.ChapterCounts = chapterCounts;

        ViewBag.TotalPopularStories = popularStories.Count;
        ViewBag.TotalTopRatedStories = topRatedStories.Count;
        ViewBag.TotalNewStories = newStories.Count;
        ViewBag.TotalCompletedStories = completedStories.Count;

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> LoadMoreStories(string category, int offset)
    {
        const int pageSize = 6;
        List<Story> stories;
        var currentUser = await _userManager.GetUserAsync(User);
        string currentUserId = currentUser?.Id;
        var sixMonthsAgo = DateTime.Now.AddMonths(-6);

        switch (category)
        {
            case "Popular":
                stories = await _context.Stories
                    .Where(s => s.IsPublic && !s.Author.IsBanned && !s.IsHidden
                        && (currentUserId == null ||
                            (!_context.Blocks.Any(b => b.UserId == currentUserId && (b.BlockedUserId == s.AuthorId || b.BlockedStoryId == s.Id)) &&
                             !_context.Blocks.Any(b => b.UserId == s.AuthorId && b.BlockedUserId == currentUserId))))
                    .Include(s => s.Author)
                    .Include(s => s.Chapters)
                    .Where(s => s.UpdatedAt >= sixMonthsAgo || s.Chapters.Any(c => c.CreatedAt >= sixMonthsAgo))
                    .Select(s => new
                    {
                        Story = s,
                        FollowerCount = _context.Follows.Count(f => f.FollowingId == s.AuthorId),
                        AverageRating = _context.Ratings.Where(r => r.StoryId == s.Id).Average(r => (double?)r.RatingValue) ?? 0
                    })
                    .OrderByDescending(x => x.FollowerCount)
                    .ThenByDescending(x => x.AverageRating)
                    .ThenByDescending(x => x.Story.UpdatedAt ?? x.Story.CreatedAt)
                    .Select(x => x.Story)
                    .Skip(offset)
                    .Take(pageSize)
                    .ToListAsync();
                break;

            case "TopRated":
                var topRatedStoriesQuery = _context.Stories
                    .Where(s => s.IsPublic && !s.Author.IsBanned && !s.IsHidden
                        && (currentUserId == null ||
                            (!_context.Blocks.Any(b => b.UserId == currentUserId && (b.BlockedUserId == s.AuthorId || b.BlockedStoryId == s.Id)) &&
                             !_context.Blocks.Any(b => b.UserId == s.AuthorId && b.BlockedUserId == currentUserId))))
                    .Include(s => s.Chapters);

                var topRatedStoriesList = await topRatedStoriesQuery.ToListAsync();
                Console.WriteLine($"LoadMoreStories - TopRated - Offset: {offset}, Total Stories Before Sorting: {topRatedStoriesList.Count}");
                Console.WriteLine($"LoadMoreStories - TopRated - Titles Before Sorting: {string.Join(", ", topRatedStoriesList.Select(s => s.Title))}");

                var orderedStories = topRatedStoriesList
                    .Select(s => new
                    {
                        Story = s,
                        AverageRating = _context.Ratings
                            .Where(r => r.StoryId == s.Id)
                            .Average(r => (double?)r.RatingValue) ?? 0
                    })
                    .OrderByDescending(x => x.AverageRating)
                    .ThenByDescending(x => x.Story.CreatedAt)
                    .Select(x => x.Story)
                    .ToList();

                Console.WriteLine($"LoadMoreStories - TopRated - Titles After Sorting (Before Skip/Take): {string.Join(", ", orderedStories.Select(s => s.Title))}");

                stories = orderedStories
                    .Skip(offset)
                    .Take(pageSize)
                    .ToList();

                Console.WriteLine($"LoadMoreStories - TopRated - Titles After Skip/Take: {string.Join(", ", stories.Select(s => s.Title))}");
                break;

            case "New":
                stories = await _context.Stories
                    .Where(s => s.IsPublic && !s.Author.IsBanned && !s.IsHidden
                        && (currentUserId == null ||
                            (!_context.Blocks.Any(b => b.UserId == currentUserId && (b.BlockedUserId == s.AuthorId || b.BlockedStoryId == s.Id)) &&
                             !_context.Blocks.Any(b => b.UserId == s.AuthorId && b.BlockedUserId == currentUserId))))
                    .Include(s => s.Chapters)
                    .OrderByDescending(s => s.CreatedAt)
                    .Skip(offset)
                    .Take(pageSize)
                    .ToListAsync();
                break;

            case "Completed":
                stories = await _context.Stories
                    .Where(s => s.IsPublic && s.IsCompleted && !s.Author.IsBanned && !s.IsHidden
                        && (currentUserId == null ||
                            (!_context.Blocks.Any(b => b.UserId == currentUserId && (b.BlockedUserId == s.AuthorId || b.BlockedStoryId == s.Id)) &&
                             !_context.Blocks.Any(b => b.UserId == s.AuthorId && b.BlockedUserId == currentUserId))))
                    .Include(s => s.Chapters)
                    .OrderByDescending(s => s.CreatedAt)
                    .Skip(offset)
                    .Take(pageSize)
                    .ToListAsync();
                break;

            default:
                return BadRequest();
        }

        var viewCounts = new Dictionary<int, int>();
        var ratings = new Dictionary<int, double>();
        var chapterCounts = new Dictionary<int, int>();

        foreach (var story in stories)
        {
            viewCounts[story.Id] = story.Chapters?.Sum(ch => ch.ViewCount) ?? 0;
            var storyRatings = await _context.Ratings
                .Where(r => r.StoryId == story.Id && !r.User.IsBanned
                    && (currentUserId == null ||
                        (!_context.Blocks.Any(b => b.UserId == currentUserId && b.BlockedUserId == r.UserId) &&
                         !_context.Blocks.Any(b => b.UserId == r.UserId && b.BlockedUserId == currentUserId))))
                .ToListAsync();
            ratings[story.Id] = storyRatings.Any() ? storyRatings.Average(r => r.RatingValue) : 0;
            chapterCounts[story.Id] = story.Chapters?.Count(ch => ch.IsPublic) ?? 0;
        }

        ViewBag.ViewCounts = viewCounts;
        ViewBag.Ratings = ratings;
        ViewBag.ChapterCounts = chapterCounts;

        return PartialView("_StoryList", stories);
    }
}
