using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.ViewModels
{
    public class ChapterSegmentCreateViewModel
    {
        public int ChapterId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề đoạn.")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Nội dung đoạn không được để trống.")]
        public string Content { get; set; }

        public IFormFile? Image { get; set; }
    }
}
