using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace InteractiveStoryWeb.ViewModels
{
    public class ChapterCreateViewModel
    {
        public int Id { get; set; }
        public int StoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề chương.")]
        public string Title { get; set; }

        public IFormFile? Image { get; set; }

        [Display(Name = "Công khai chương này")]
        public bool IsPublic { get; set; }
    }
}
