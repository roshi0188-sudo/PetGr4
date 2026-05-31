using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data; // Đảm bảo trỏ đúng đến nơi chứa ApplicationDbContext
using PetSocial.ViewModels;

namespace PetSocial.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Tiêm AppDbContext để gọi Database
        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Đếm tổng số liệu thật từ các bảng
            var totalUsers = await _context.Users.CountAsync();
            var totalPets = await _context.Pets.CountAsync();
            var totalPosts = await _context.Posts.CountAsync();
            
            // (Nếu Như và Anh chưa làm xong bảng Comments, Likes, Messages thì cứ tạm để 0)
            var totalComments = 0; 
            var totalLikes = 0;
            var totalMessages = 0;

            // 2. Kéo danh sách User từ Database (Seed Data) và map sang ViewModel
            var usersList = await _context.Users
                .Select(u => new UserDashboardVM
                {
                    FullName = u.FullName,
                    Email = u.Email,
                    JoinDate = u.CreatedAt, // Lấy ngày tạo thật từ DB
                    
                    // Đếm số lượng pet và post của từng người
                    PetCount = _context.Pets.Count(p => p.UserId == u.Id),
                    PostCount = _context.Posts.Count(p => p.UserId == u.Id),
                    
                    // Kiểm tra xem tài khoản có đang bị khóa (Ban) hay không
                    IsActive = !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTimeOffset.Now,
                    
                    // Tạm thời gán Role hiển thị (Do bảng Roles của Identity nằm riêng)
                    Role = u.Email == "admin@petsocial.com" ? "Admin" : "User"
                })
                .OrderByDescending(u => u.JoinDate) // Xếp người mới đăng ký lên đầu
                .ToListAsync();

            // 3. Đổ dữ liệu thật vào ViewModel
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