using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace InteractiveStoryWeb.ViewModels
{
    public class StoryCustomizationViewModel
    {
        public int StoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên nhân vật.")]
        [Display(Name = "Tên nhân vật")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập cách xưng hô thứ nhất.")]
        [Display(Name = "Xưng hô thứ nhất (tôi, mình, tớ,...)")]
        public string FirstPersonPronoun { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập cách xưng hô thứ hai.")]
        [Display(Name = "Xưng hô thứ hai (cô, anh, họ,...)")]
        public string SecondPersonPronoun { get; set; }
    }
}
