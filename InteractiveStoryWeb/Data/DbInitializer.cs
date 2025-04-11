using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using InteractiveStoryWeb.Models;

namespace InteractiveStoryWeb.Data
{
    public static class DbInitializer
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Tạo roles nếu chưa có
            string[] roles = { "Admin", "User" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Tạo lại admin
            string adminEmail = "luongmt66@gmail.com";
            string adminPassword = "Admin@123";

            var existingUser = await userManager.FindByEmailAsync(adminEmail);
            if (existingUser == null)
            {
                var newAdmin = new ApplicationUser
                {
                    UserName = "Tuyen",
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(newAdmin, adminPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                }
            }
            else
            {
                // Nếu đã tồn tại, đảm bảo đã gán quyền Admin
                if (!await userManager.IsInRoleAsync(existingUser, "Admin"))
                {
                    await userManager.AddToRoleAsync(existingUser, "Admin");
                }

                // Nếu chưa xác nhận email
                if (!await userManager.IsEmailConfirmedAsync(existingUser))
                {
                    var token = await userManager.GenerateEmailConfirmationTokenAsync(existingUser);
                    await userManager.ConfirmEmailAsync(existingUser, token);
                }
            }
        }
    }
}
