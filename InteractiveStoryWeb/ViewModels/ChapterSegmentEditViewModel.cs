using InteractiveStoryWeb.Models;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.ViewModels
{
    public class ChapterSegmentEditViewModel
    {
        public int Id { get; set; }

        public int ChapterId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề đoạn.")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Nội dung đoạn không được để trống.")]
        public string Content { get; set; }

        public string? ImageUrl { get; set; }

        public IFormFile? NewImage { get; set; }

        public ImagePosition ImagePosition { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
