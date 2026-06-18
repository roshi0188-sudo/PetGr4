using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace PetSocial.Controllers
{
    public class PostController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public PostController(ApplicationDbContext context, UserManager<AppUser> userManager, IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        // 1. TRANG CHỦ: HIỂN THỊ TOÀN BỘ BÀI VIẾT CỘNG ĐỒNG
        public async Task<IActionResult> Index()
        {
            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments).ThenInclude(c => c.User)
                .Include(p => p.Likes)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            await LoadFollowingIdsAsync();

            ViewData["ActiveMenu"] = "Home";
            ViewBag.PageTitle = "Bài viết mới nhất";

            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            ViewBag.IsAdmin = User.IsInRole("Admin");

            return View(posts);
        }

        // ====== BÀI VIẾT CỦA TÔI ======
        [Authorize]
        public async Task<IActionResult> MyPosts()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var myPosts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments).ThenInclude(c => c.User)
                .Include(p => p.Likes)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            await LoadFollowingIdsAsync();

            ViewData["ActiveMenu"] = "MyPost";
            ViewBag.PageTitle = "Bài viết của tôi";

            ViewBag.CurrentUserId = userId;
            ViewBag.IsAdmin = User.IsInRole("Admin");

            return View("Index", myPosts);
        }

        // 2. HIỂN THỊ FORM TẠO BÀI VIẾT MỚI (GET)
        [Authorize]
        public async Task<IActionResult> Create(string? returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            var model = new Post();
            if (user != null)
            {
                model.User = user;
                model.UserId = user.Id;
            }
            ViewBag.ReturnUrl = returnUrl ?? Request.Headers["Referer"].ToString();
            return View(model);
        }

        // 3. XỬ LÝ TẠO BÀI VIẾT MỚI (POST)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Post model, IFormFile? imageFile, string? returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            model.UserId = user.Id;
            model.CreatedAt = DateTime.Now;

            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("ImageUrl");

            if (!ModelState.IsValid)
            {
                model.User = user;
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                var imageUrl = await SavePostImageAsync(imageFile);
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    model.ImageUrl = imageUrl;
                }
            }

            _context.Posts.Add(model);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Index));
        }

        // 4. HIỂN THỊ FORM CẬP NHẬT BÀI VIẾT (GET)
        [Authorize]
        public async Task<IActionResult> Edit(int id, string? returnUrl)
        {
            var post = await _context.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();
            if (!await CanManagePostAsync(post)) return Forbid();

            ViewBag.ReturnUrl = returnUrl ?? Request.Headers["Referer"].ToString();
            return View(post);
        }

        // 5. XỬ LÝ CẬP NHẬT BÀI VIẾT (POST)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Post formModel, IFormFile? imageFile, string? returnUrl)
        {
            if (id != formModel.Id) return BadRequest();

            var post = await _context.Posts.FindAsync(id);
            if (post == null) return NotFound();
            if (!await CanManagePostAsync(post)) return Forbid();

            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("ImageUrl");

            if (!ModelState.IsValid)
            {
                ViewBag.ReturnUrl = returnUrl;
                return View(formModel);
            }

            post.Content = formModel.Content ?? string.Empty;

            if (imageFile != null && imageFile.Length > 0)
            {
                var newImageUrl = await SavePostImageAsync(imageFile);
                if (!string.IsNullOrWhiteSpace(newImageUrl))
                {
                    DeleteLocalImage(post.ImageUrl);
                    post.ImageUrl = newImageUrl;
                }
            }

            _context.Update(post);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Index));
        }

        // 6. HIỂN THỊ FORM XÁC NHẬN XÓA BÀI VIẾT (GET)
        [Authorize]
        public async Task<IActionResult> Delete(int id, string? returnUrl)
        {
            var post = await _context.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();
            if (!await CanManagePostAsync(post)) return Forbid();

            ViewBag.ReturnUrl = returnUrl ?? Request.Headers["Referer"].ToString();
            return View(post);
        }

        // 7. XỬ LÝ XOÁ BÀI VIẾT (POST)
        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string? returnUrl)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null) return NotFound();
            if (!await CanManagePostAsync(post)) return Forbid();

            DeleteLocalImage(post.ImageUrl);

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Index));
        }

        // 8. CHI TIẾT BÀI VIẾT (GET)
        public async Task<IActionResult> Details(int id)
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments).ThenInclude(c => c.User)
                .Include(p => p.Likes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();

            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            ViewBag.IsAdmin = User.IsInRole("Admin");

            return View(post);
        }

        private async Task<bool> CanManagePostAsync(Post post)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;
            return post.UserId == user.Id || User.IsInRole("Admin");
        }

        private async Task<string?> SavePostImageAsync(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) return null;
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "posts");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var extension = Path.GetExtension(imageFile.FileName);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await imageFile.CopyToAsync(stream);

            return $"/images/posts/{fileName}";
        }

        private void DeleteLocalImage(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/images/posts/")) return;
            var fullPath = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/'));
            if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleLike(int postId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var existingLike = await _context.Likes.FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == user.Id);
            bool isLikedNow = false;

            if (existingLike != null) _context.Likes.Remove(existingLike);
            else { _context.Likes.Add(new Like { PostId = postId, UserId = user.Id }); isLikedNow = true; }

            await _context.SaveChangesAsync();
            var totalLikes = await _context.Likes.CountAsync(l => l.PostId == postId);
            return Json(new { success = true, isLiked = isLikedNow, count = totalLikes });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCommentAjax(int postId, string commentContent)
        {
            if (string.IsNullOrWhiteSpace(commentContent)) return Json(new { success = false });
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false });

            var newComment = new Comment { PostId = postId, UserId = user.Id, Content = commentContent, CreatedAt = DateTime.Now };
            _context.Comments.Add(newComment);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                id = newComment.Id,
                content = newComment.Content,
                createdAt = newComment.CreatedAt.ToString("dd/MM HH:mm"),
                authorName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.UserName,
                avatarUrl = user.AvatarUrl ?? ""
            });
        }

        private async Task LoadFollowingIdsAsync()
        {
            var currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(currentUserId)) { ViewData["FollowingIds"] = new HashSet<string>(); return; }
            var followingIds = await _context.Follows.Where(f => f.FollowerId == currentUserId).Select(f => f.FollowingId).ToListAsync();
            ViewData["FollowingIds"] = new HashSet<string>(followingIds);
        }
    }
}