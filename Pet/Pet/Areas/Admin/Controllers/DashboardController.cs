using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PetSocial.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] // Bảo mật tuyệt đối: Chỉ Admin mới được vào
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. TÍNH TOÁN CÁC CON SỐ THỐNG KÊ TỔNG QUAN NGOÀI DATABASE
            var totalUsers = await _context.Users.CountAsync();
            var totalPets = await _context.Pets.CountAsync();
            var totalPosts = await _context.Posts.CountAsync();

            // Lấy dữ liệu thật từ bảng Comments và Likes để thẻ Tương tác có số liệu thực
            var totalComments = await _context.Comments.CountAsync();
            var totalLikes = await _context.Likes.CountAsync();
            var totalMessages = 0; // Giữ nguyên 0 nếu nhóm bạn chưa làm tính năng Chat

            // (ĐÃ XÓA VÒNG LẶP FOREACH LẤY DANH SÁCH USER VÌ CHÚNG TA ĐÃ TÁCH SANG TRANG QUẢN LÝ USER RIÊNG)
            // Việc này giúp Dashboard load siêu nhanh do không bị vướng lỗi N+1 Query.

            // 2. DỮ LIỆU CHO BIỂU ĐỒ ĐƯỜNG (LINE CHART) - 7 NGÀY GẦN NHẤT
            var weeklyLabels = new List<string>();
            var weeklyNewUsers = new List<int>();
            var weeklyNewPosts = new List<int>();

            for (int d = 6; d >= 0; d--)
            {
                var day = DateTime.Today.AddDays(-d);
                weeklyLabels.Add(day.ToString("dd/MM")); // Format dd/MM cho đẹp mắt

                weeklyNewUsers.Add(await _context.Users.CountAsync(u => u.CreatedAt.Date == day));
                weeklyNewPosts.Add(await _context.Posts.CountAsync(p => p.CreatedAt.Date == day));
            }

            // 3. DỮ LIỆU CHO BIỂU ĐỒ CỘT (BAR CHART) - 6 THÁNG GẦN NHẤT
            var monthlyLabels = new List<string>();
            var monthlyNewUsers = new List<int>();
            var monthlyNewPosts = new List<int>();

            for (int m = 5; m >= 0; m--)
            {
                var month = DateTime.Today.AddMonths(-m);
                monthlyLabels.Add(month.ToString("MM/yyyy"));

                var firstOfMonth = new DateTime(month.Year, month.Month, 1);
                var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);

                monthlyNewUsers.Add(await _context.Users.CountAsync(u => u.CreatedAt.Date >= firstOfMonth && u.CreatedAt.Date <= lastOfMonth));
                monthlyNewPosts.Add(await _context.Posts.CountAsync(p => p.CreatedAt.Date >= firstOfMonth && p.CreatedAt.Date <= lastOfMonth));
            }

            // 4. ĐÓNG GÓI DỮ LIỆU CHUYỂN SANG VIEW
            var model = new DashboardVM
            {
                TotalUsers = totalUsers,
                TotalPets = totalPets,
                TotalPosts = totalPosts,
                TotalComments = totalComments,
                TotalLikes = totalLikes,
                TotalMessages = totalMessages,

                WeeklyLabels = weeklyLabels,
                WeeklyNewUsers = weeklyNewUsers,
                WeeklyNewPosts = weeklyNewPosts,

                MonthlyLabels = monthlyLabels,
                MonthlyNewUsers = monthlyNewUsers,
                MonthlyNewPosts = monthlyNewPosts
            };

            return View(model);
        }
    }
}