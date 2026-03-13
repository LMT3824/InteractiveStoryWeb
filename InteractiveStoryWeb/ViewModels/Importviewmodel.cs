using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.ViewModels
{
    public class ImportFileViewModel
    {
        public int StoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn file để tải lên.")]
        [Display(Name = "File truyện")]
        public IFormFile File { get; set; }
    }

    public class ImportPreviewViewModel
    {
        public int StoryId { get; set; }
        public string StoryTitle { get; set; }
        public List<ChapterPreviewModel> Chapters { get; set; } = new List<ChapterPreviewModel>();
    }

    public class ChapterPreviewModel
    {
        public string Title { get; set; }
        public List<SegmentPreviewModel> Segments { get; set; } = new List<SegmentPreviewModel>();
        public bool IsSelected { get; set; } = true; // Mặc định chọn tất cả
    }

    public class SegmentPreviewModel
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public bool IsSelected { get; set; } = true; // Mặc định chọn tất cả
    }

    public class ImportConfirmViewModel
    {
        public int StoryId { get; set; }
        public string SerializedData { get; set; } // JSON của ImportPreviewViewModel
        public List<int> SelectedChapters { get; set; } = new List<int>(); // Index của chapters được chọn
    }
}