using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.Models
{
    public class Choice
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Chương gốc không hợp lệ.")]
        public int ChapterId { get; set; }

        [Required(ErrorMessage = "Nội dung lựa chọn là bắt buộc.")]
        public string ChoiceText { get; set; } = "";

        [Required(ErrorMessage = "Chương tiếp theo là bắt buộc.")]
        public int NextChapterId { get; set; }

        public Chapter Chapter { get; set; }
        public Chapter? NextChapter { get; set; } // 👈 Navigation quan trọng
    }
}
