using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.ViewModels
{
    public class ChoiceCreateViewModel
    {
        public int ChapterId { get; set; }

        [Required(ErrorMessage = "Bạn phải nhập nội dung lựa chọn.")]
        [Display(Name = "Nội dung lựa chọn")]
        public string ChoiceText { get; set; }
    }
}
