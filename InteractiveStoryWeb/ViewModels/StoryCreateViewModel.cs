using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.ViewModels
{
    public class StoryCreateViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tiêu đề là bắt buộc.")]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Mô tả là bắt buộc.")]
        [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
        [Display(Name = "Mô tả")]
        public string Description { get; set; }

        [Display(Name = "Thể loại")]
        [Required(ErrorMessage = "Vui lòng chọn thể loại.")]
        public string Genre { get; set; }

        [Display(Name = "Ảnh bìa (tuỳ chọn)")]
        public IFormFile? CoverImage { get; set; }

        [Display(Name = "Công khai truyện")]
        public bool IsPublic { get; set; } = true;

        [Display(Name = "Cho phép người đọc tùy chỉnh tên và xưng hô")]
        public bool AllowCustomization { get; set; }

        [Display(Name = "Truyện đã hoàn thành")]
        public bool IsCompleted { get; set; }
    }
}
