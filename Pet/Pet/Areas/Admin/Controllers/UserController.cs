using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.Models;
using PetSocial.ViewModels;

namespace PetSocial.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UserController(UserManager<AppUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // 1. HIỂN THỊ DANH SÁCH TÀI KHOẢN KÈM TÌM KIẾM, LỌC, SẮP XẾP VÀ PHÂN TRANG
        [HttpGet]
        public async Task<IActionResult> Index(string? searchString, string? filter = "all", string? sort = "newest", int page = 1)
        {
            const int pageSize = 20; // fixed per user request
            filter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.ToLowerInvariant();
            sort = sort == "oldest" ? "oldest" : "newest";

            var query = _userManager.Users.AsQueryable();

            // Tìm kiếm
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();
                var lowerSearch = searchString.ToLower();
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.ToLower().Contains(lowerSearch)) ||
                    (u.Email != null && u.Email.ToLower().Contains(lowerSearch)));
            }

            // Bộ lọc: all, active, suspended
            if (filter != "all")
            {
                if (filter == "active")
                {
                    query = query.Where(u => !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTimeOffset.Now);
                }
                else if (filter == "suspended")
                {
                    query = query.Where(u => u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.Now);
                }
            }

            // Sắp xếp
            query = sort == "oldest" ? query.OrderBy(u => u.CreatedAt) : query.OrderByDescending(u => u.CreatedAt);

            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var usersFromDb = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var userVMs = new List<UserManageVM>();

            foreach (var user in usersFromDb)
            {
                var petCount = await _context.Pets.CountAsync(p => p.UserId == user.Id);
                var postCount = await _context.Posts.CountAsync(p => p.UserId == user.Id);
                var removedPostViolationCount = await _context.Posts.CountAsync(p => p.UserId == user.Id && p.IsRemovedByAi);
                var blockedCommentViolationCount = await _context.Notifications.CountAsync(n =>
                    n.UserId == user.Id &&
                    n.Title.Contains("AI đã chặn bình luận vi phạm"));
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                bool isActive = !user.LockoutEnd.HasValue || user.LockoutEnd <= DateTimeOffset.Now;

                userVMs.Add(new UserManageVM
                {
                    Id = user.Id,
                    FullName = user.FullName ?? "Chưa cập nhật",
                    Email = user.Email ?? "",
                    JoinDate = user.CreatedAt,
                    AvatarUrl = user.AvatarUrl ?? "",
                    PetCount = petCount,
                    PostCount = postCount,
                    ViolationCount = removedPostViolationCount + blockedCommentViolationCount,
                    IsActive = isActive,
                    IsAdmin = isAdmin
                });
            }

            ViewBag.SearchString = searchString;
            ViewBag.Filter = filter;
            ViewBag.Sort = sort;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            // Global stats (not limited to current page)
            ViewBag.TotalUsers = await _userManager.Users.CountAsync();
            ViewBag.ActiveUsers = await _userManager.Users.CountAsync(u => !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTimeOffset.Now);
            ViewBag.SuspendedUsers = await _userManager.Users.CountAsync(u => u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.Now);
            ViewBag.NewThisMonth = await _userManager.Users.CountAsync(u => u.CreatedAt.Month == DateTime.Now.Month && u.CreatedAt.Year == DateTime.Now.Year);
            ViewBag.TotalPages = totalPages;

            return View(userVMs);
        }

        // 2. KHÓA / MỞ KHÓA TÀI KHOẢN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBlock(string id, string? searchString, string? filter, string? sort, int page = 1)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var routeValues = new
            {
                searchString,
                filter,
                sort,
                page
            };

            // Chống Admin tự khóa chính mình
            var currentUser = await _userManager.GetUserAsync(User);
            if (user.Id == currentUser?.Id)
            {
                TempData["ErrorMessage"] = "Bạn không thể tự khóa tài khoản của chính mình!";
                return RedirectToAction(nameof(Index), routeValues);
            }

            if (user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.Now)
                user.LockoutEnd = DateTimeOffset.Now.AddYears(100); // Khóa
            else
                user.LockoutEnd = null; // Mở khóa

            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = $"Đã thay đổi trạng thái tài khoản của {user.FullName}.";

            return RedirectToAction(nameof(Index), routeValues);
        }

        // Role changes from admin UI are disabled per policy: admins must not change roles here.
    }
}
