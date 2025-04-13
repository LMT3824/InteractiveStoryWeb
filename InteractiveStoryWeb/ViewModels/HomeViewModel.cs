using InteractiveStoryWeb.Models;
using System.Collections.Generic;

namespace InteractiveStoryWeb.ViewModels
{
    public class HomeViewModel
    {
        public List<Story> TopViewed { get; set; }
        public List<Story> NewStories { get; set; }
        public List<Story> CompletedStories { get; set; }
    }
}