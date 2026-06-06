using Microsoft.AspNetCore.Identity;
using PetSocial.Models;

namespace PetSocial.Data
{
    public static class RoleSeeder
    {
        // Hàm này nhận vào IServiceProvider để lấy các dịch vụ của hệ thống
        public static async Task SeedRolesAndUsersAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();

            // 1. Tạo các quyền cơ bản
            string[] roleNames = { "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Gán quyền Admin
            var adminUser = await userManager.FindByEmailAsync("admin@petsocial.com");
            if (adminUser != null && !await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // 3. Gán quyền User cho các thành viên nhóm
            string[] memberEmails = { "lam@petsocial.com", "ngoc@petsocial.com", "anh@petsocial.com", "nhu@petsocial.com" };
            foreach (var email in memberEmails)
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user != null && !await userManager.IsInRoleAsync(user, "User"))
                {
                    await userManager.AddToRoleAsync(user, "User");
                }
            }
        }
    }
}