using System;
using System.Collections.Generic;

namespace InteractiveStoryWeb.ViewModels
{
    public class ChapterTreeViewModel
    {
        public int Id { get; set; }
        public string ContentPreview { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ChapterTreeViewModel> Children { get; set; } = new List<ChapterTreeViewModel>();
    }
}
