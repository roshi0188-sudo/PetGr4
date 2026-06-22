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
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            
            var totalUsers = await _context.Users.CountAsync();
            var totalPets = await _context.Pets.CountAsync();
            var totalPosts = await _context.Posts.CountAsync();

            
            var totalComments = await _context.Comments.CountAsync();
            var totalLikes = await _context.Likes.CountAsync();
            var totalMessages = 0;

            var weeklyLabels = new List<string>();
            var weeklyNewUsers = new List<int>();
            var weeklyNewPosts = new List<int>();

            for (int d = 6; d >= 0; d--)
            {
                var day = DateTime.Today.AddDays(-d);
                weeklyLabels.Add(day.ToString("dd/MM")); 

                weeklyNewUsers.Add(await _context.Users.CountAsync(u => u.CreatedAt.Date == day));
                weeklyNewPosts.Add(await _context.Posts.CountAsync(p => p.CreatedAt.Date == day));
            }

          
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

            //  ĐÓNG GÓI DỮ LIỆU CHUYỂN SANG VIEW
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