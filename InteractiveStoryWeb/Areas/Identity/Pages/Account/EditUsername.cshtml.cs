using InteractiveStoryWeb.Models;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InteractiveStoryWeb.Areas.Identity.Pages.Account
{
    public class EditUsernameModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public EditUsernameModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public EditUsernameInputModel Input { get; set; }

        public class EditUsernameInputModel
        {
            [Required(ErrorMessage = "Tên người dùng là bắt buộc.")]
            [Display(Name = "Tên người dùng mới")]
            public string NewUsername { get; set; }

            [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc.")]
            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            public string Password { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, Input.Password);
            if (!passwordValid)
            {
                ModelState.AddModelError("Input.Password", "Mật khẩu không đúng.");
                return Page();
            }

            user.UserName = Input.NewUsername;
            user.NormalizedUserName = Input.NewUsername.ToUpper();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["StatusMessage"] = "Đổi tên người dùng thành công.";

            return RedirectToAction("MyProfile", "Account");
        }
    }
}
