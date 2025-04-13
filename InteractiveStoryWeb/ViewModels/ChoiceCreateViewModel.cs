using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.ViewModels
{
    public class ChoiceCreateViewModel
    {
        public int ChapterSegmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung lựa chọn.")]
        public string ChoiceText { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ID đoạn tiếp theo.")]
        public int NextSegmentId { get; set; }
    }
}
