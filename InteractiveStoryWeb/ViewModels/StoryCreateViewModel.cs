using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.ViewModels
{
    public class StoryCreateViewModel
    {
        [Required(ErrorMessage = "Tiêu đề là bắt buộc.")]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Mô tả là bắt buộc.")]
        [Display(Name = "Mô tả")]
        public string Description { get; set; }

        [Display(Name = "Thể loại")]
        [Required(ErrorMessage = "Vui lòng chọn thể loại.")]
        public string Genre { get; set; }

        [Display(Name = "Ảnh bìa (tuỳ chọn)")]
        public IFormFile? CoverImage { get; set; }

        [Display(Name = "Công khai?")]
        public bool IsPublic { get; set; } = true;
    }
}
