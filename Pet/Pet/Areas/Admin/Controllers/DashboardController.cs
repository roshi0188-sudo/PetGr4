using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data; // Thừa hưởng ApplicationDbContext từ project của bạn
using PetSocial.ViewModels;

namespace PetSocial.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Tiêm ApplicationDbContext thật của bạn vào đây
        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Tính toán các con số thống kê thực tế ngoài Database
            var totalUsers = await _context.Users.CountAsync();
            var totalPets = await _context.Pets.CountAsync();
            var totalPosts = await _context.Posts.CountAsync();

            // Tạm thời gán bằng 0 nếu các bảng này chưa được team hoàn thiện
            var totalComments = 0;
            var totalLikes = 0;
            var totalMessages = 0;

            // 2. Lấy danh sách thành viên thực tế (Từ hạt giống SeedData của bạn)
            var usersFromDb = await _context.Users.ToListAsync();
            var usersList = new List<UserDashboardVM>();

            foreach (var user in usersFromDb)
            {
                // Đếm số lượng Pet thật của User này dựa theo mã UserId khóa ngoại
                var petCount = await _context.Pets.CountAsync(p => p.UserId == user.Id);

                // Đếm số lượng Bài viết thật của User này (Ráp nối dữ liệu tiến độ của Anh sau này)
                var postCount = await _context.Posts.CountAsync(p => p.UserId == user.Id);

                // Kiểm tra tài khoản có đang bị khóa (Lockout) hay không
                bool isActive = !user.LockoutEnd.HasValue || user.LockoutEnd <= DateTimeOffset.Now;

                // Gán hiển thị Role tạm thời dựa theo Email để bảng hiển thị đẹp mắt
                string displayRole = user.Email == "admin@petsocial.com" ? "Admin" : "User";

                usersList.Add(new UserDashboardVM
                {
                    FullName = user.FullName ?? "Chưa cập nhật",
                    Email = user.Email,
                    JoinDate = DateTime.Now.AddDays(-5), // Hoặc dùng thuộc tính ngày tạo của bạn nếu có
                    PetCount = petCount,
                    PostCount = postCount,
                    Role = displayRole,
                    IsActive = isActive
                });
            }

            // 3. Đóng gói dữ liệu thật ném qua cho View hiển thị
            var model = new DashboardVM
            {
                TotalUsers = totalUsers,
                TotalPets = totalPets,
                TotalPosts = totalPosts,
                TotalComments = totalComments,
                TotalLikes = totalLikes,
                TotalMessages = totalMessages,
                Users = usersList
            };

            return View(model);
        }
    }
}