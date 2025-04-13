using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace InteractiveStoryWeb.ViewModels
{
    public class ChapterCreateViewModel
    {
        public int StoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề chương.")]
        public string Title { get; set; }

        [Display(Name = "Tiêu đề đoạn đầu tiên")]
        [Required(ErrorMessage = "Tiêu đề đoạn đầu tiên là bắt buộc.")]
        public string FirstSegmentTitle { get; set; }

        [Required(ErrorMessage = "Nội dung đoạn đầu tiên không được để trống.")]
        public string FirstSegmentContent { get; set; }

        public IFormFile? Image { get; set; }

        [Display(Name = "Công khai chương này")]
        public bool IsPublic { get; set; }
    }
}
