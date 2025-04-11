using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.ViewModels
{
    public class ChapterCreateViewModel
    {
        public int StoryId { get; set; }

        [Display(Name = "ID chương gốc (nếu đây là nhánh)")]
        public int? ParentChapterId { get; set; }

        [Required(ErrorMessage = "Nội dung chương là bắt buộc.")]
        [Display(Name = "Nội dung chương")]
        public string Content { get; set; }

        [Display(Name = "Ảnh giữa chương (nếu có)")]
        public IFormFile? Image { get; set; }
    }
}
