using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.Models;
using PetSocial.ViewModels;

namespace PetSocial.Controllers
{
    [Authorize] // Bắt buộc đăng nhập mới được vào Controller này
    public class ProfileController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _context;

        public ProfileController(UserManager<AppUser> userManager, IWebHostEnvironment webHostEnvironment, ApplicationDbContext context)
        {
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment; // Dùng để lấy đường dẫn lưu ảnh
            _context = context;
        }

        // GET: /Profile/Index
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            return RedirectToAction(nameof(Details), new { id = user.Id });
        }

        // GET: /Profile/Details/{id} - Xem hồ sơ người dùng khác kèm Follow/Followers/Following
        [AllowAnonymous]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var profileUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (profileUser == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            var followerCount = await _context.Follows.CountAsync(f => f.FollowingId == id);
            var followingCount = await _context.Follows.CountAsync(f => f.FollowerId == id);
            var postCount = await _context.Posts.CountAsync(p => p.UserId == id && !p.IsRemovedByAi);
            var pets = await _context.Pets
                .Include(p => p.User)
                .Where(p => p.UserId == id)
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            bool isFollowing = !string.IsNullOrEmpty(currentUserId) &&
                await _context.Follows.AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == id);

            // ĐÃ SỬA: Thêm .ThenInclude(c => c.User) để nạp thông tin tài khoản của người bình luận
            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User) // BẮT BUỘC PHẢI CÓ DÒNG NÀY để hiển thị tên thật thay vì "Thành viên"
                .Include(p => p.Likes)
                    .ThenInclude(l => l.User)
                .Where(p => p.UserId == id && !p.IsRemovedByAi)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var model = new ProfileDetails
            {
                User = profileUser,
                FollowerCount = followerCount,
                FollowingCount = followingCount,
                PostCount = postCount,
                IsOwnProfile = !string.IsNullOrEmpty(currentUserId) && currentUserId == id,
                IsFollowing = isFollowing,
                Posts = posts,
                Pets = pets
            };

            return View(model);
        }

        // GET: /Profile/Followers/{id} - Danh sách người theo dõi
        [AllowAnonymous]
        public async Task<IActionResult> Followers(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var profileUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (profileUser == null) return NotFound();

            var followers = await _context.Follows
                .Where(f => f.FollowingId == id)
                .Include(f => f.Follower)
                .Select(f => f.Follower)
                .ToListAsync();

            ViewData["ProfileUser"] = profileUser;
            ViewData["ListTitle"] = "Người theo dõi";

            return View("FollowList", followers);
        }

        // GET: /Profile/FollowingList/{id} - Danh sách đang theo dõi
        [AllowAnonymous]
        public async Task<IActionResult> FollowingList(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var profileUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (profileUser == null) return NotFound();

            var following = await _context.Follows
                .Where(f => f.FollowerId == id)
                .Include(f => f.Following)
                .Select(f => f.Following)
                .ToListAsync();

            ViewData["ProfileUser"] = profileUser;
            ViewData["ListTitle"] = "Đang theo dõi";

            return View("FollowList", following);
        }

        // GET: /Profile/Edit
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var model = new UserProfileVM
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber,
                CurrentAvatarUrl = user.AvatarUrl
            };

            return View(model);
        }

        // POST: /Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserProfileVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // Cập nhật thông tin cơ bản
            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;

            // Xử lý Upload Avatar
            if (model.AvatarFile != null && model.AvatarFile.Length > 0)
            {
                // 1. Tạo thư mục nếu chưa có
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "avatars");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // 2. Tạo tên file ngẫu nhiên để tránh trùng lặp
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.AvatarFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // 3. Lưu file vào server
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.AvatarFile.CopyToAsync(fileStream);
                }

                // 4. Cập nhật đường dẫn vào database
                user.AvatarUrl = "/images/avatars/" + uniqueFileName;
            }

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công!";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }
    }
}
