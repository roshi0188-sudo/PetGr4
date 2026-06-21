using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.Models;
using PetSocial.Services;
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
        private readonly IPetAiService _petAiService;

        public PostController(ApplicationDbContext context, UserManager<AppUser> userManager, IWebHostEnvironment environment, IPetAiService petAiService)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
            _petAiService = petAiService;
        }

        // 1. TRANG CHỦ: HIỂN THỊ TOÀN BỘ BÀI VIẾT CỘNG ĐỒNG
        public async Task<IActionResult> Index()
        {
            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User) // ĐÃ CẬP NHẬT: Nạp thông tin người dùng cho bình luận
                .Include(p => p.Likes)
                .Where(p => !p.IsRemovedByAi)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            await LoadFollowingIdsAsync();

            ViewData["ActiveMenu"] = "Home";
            ViewBag.PageTitle = "Bài viết mới nhất";
            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            ViewBag.IsAdmin = User.IsInRole("Admin");

            return View(posts);
        }

        public Task<IActionResult> Community()
        {
            return Index();
        }

        // ====== THÊM MỚI NGHIỆP VỤ SIDEBAR: BÀI VIẾT CỦA TÔI ======
        [Authorize]
        public async Task<IActionResult> MyPosts()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            // Lọc chính xác các bài viết do tài khoản đang đăng nhập tạo ra
            var myPosts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User) // ĐÃ CẬP NHẬT: Nạp thông tin người dùng cho bình luận
                .Include(p => p.Likes)
                .Where(p => p.UserId == userId && !p.IsRemovedByAi)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            await LoadFollowingIdsAsync();

            // Đánh dấu menu kích hoạt trên Sidebar gốc
            ViewData["ActiveMenu"] = "MyPost";
            ViewBag.PageTitle = "Bài viết của tôi";
            ViewBag.CurrentUserId = userId;
            ViewBag.IsAdmin = User.IsInRole("Admin");

            // Tái sử dụng lại giao diện Index của Post để không cần viết lại giao diện mới
            return View("Index", myPosts);
        }
        // =========================================================

        // 1B. NEWS FEED - CHỈ HIỂN THỊ BÀI VIẾT CỦA NHỮNG NGƯỜI MÌNH ĐANG THEO DÕI
        [Authorize]
        public async Task<IActionResult> Feed()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var followingIds = await _context.Follows
                .Where(f => f.FollowerId == user.Id)
                .Select(f => f.FollowingId)
                .ToListAsync();

            followingIds.Add(user.Id);

            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User) // ĐÃ CẬP NHẬT: Nạp thông tin người dùng cho bình luận
                .Include(p => p.Likes)
                .Where(p => followingIds.Contains(p.UserId) && !p.IsRemovedByAi)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            await LoadFollowingIdsAsync();

            ViewData["IsFeed"] = true;
            ViewBag.PageTitle = "Bảng tin đang theo dõi";
            ViewBag.CurrentUserId = user.Id;
            ViewBag.IsAdmin = User.IsInRole("Admin");

            return View("Index", posts);
        }

        // 2. HIỂN THỊ CHI TIẾT BÀI VIẾT
        public async Task<IActionResult> Details(int id)
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments).ThenInclude(c => c.User)
                .Include(p => p.Likes)
                .FirstOrDefaultAsync(p => p.Id == id && (!p.IsRemovedByAi || User.IsInRole("Admin")));

            if (post == null) return NotFound();

            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            ViewBag.IsAdmin = User.IsInRole("Admin");

            return View(post);
        }

        // 3. HIỂN THỊ FORM TẠO BÀI VIẾT MỚI
        [Authorize]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            var model = new Post();
            if (user != null)
            {
                model.User = user;
                model.UserId = user.Id;
            }
            return View(model);
        }

        // 4. XỬ LÝ TẠO BÀI VIẾT MỚI
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Post model, IFormFile? imageFile)
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
                return View(model);
            }

            var moderation = await _petAiService.CheckContentAsync(model.Content, imageFile);
            if (moderation.IsFlagged || moderation.IsSpam)
            {
                await SaveRejectedPostForAdminReviewAsync(model, user, moderation);

                ModelState.AddModelError(
                    nameof(Post.Content),
                    "Đăng bài không thành công vì nội dung có dấu hiệu vi phạm tiêu chuẩn cộng đồng.");
                model.User = user;
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

            return RedirectToAction(nameof(MyPosts));
        }

        // 5. HIỂN THỊ FORM CẬP NHẬT BÀI VIẾT
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var post = await _context.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id && !p.IsRemovedByAi);
            if (post == null) return NotFound();

            if (!await CanManagePostAsync(post)) return Forbid();

            return View(post);
        }

        // 6. XỬ LÝ CẬP NHẬT BÀI VIẾT
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Post formModel, IFormFile? imageFile)
        {
            if (id != formModel.Id) return BadRequest();

            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id && !p.IsRemovedByAi);
            if (post == null) return NotFound();
            if (!await CanManagePostAsync(post)) return Forbid();

            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("ImageUrl");

            if (!ModelState.IsValid)
            {
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

            return RedirectToAction(nameof(MyPosts));
        }

        // 7. HIỂN THỊ FORM XÁC NHẬN XÓA BÀI VIẾT
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id && !p.IsRemovedByAi);
            if (post == null) return NotFound();
            if (!await CanManagePostAsync(post)) return Forbid();

            return View(post);
        }

        // 8. XỬ LÝ XOÁ BÀI VIẾT
        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id && !p.IsRemovedByAi);
            if (post == null) return NotFound();
            if (!await CanManagePostAsync(post)) return Forbid();

            DeleteLocalImage(post.ImageUrl);

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyPosts));
        }

        // PHÂN QUYỀN TRUY CẬP: Chỉ chủ sở hữu bài viết hoặc Admin mới được phép can thiệp sâu
        private async Task<bool> CanManagePostAsync(Post post)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;

            return post.UserId == user.Id || User.IsInRole("Admin");
        }

        private async Task<string?> SavePostImageAsync(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) return null;
            if (string.IsNullOrWhiteSpace(imageFile.ContentType) || !imageFile.ContentType.StartsWith("image/"))
                return null;
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "posts");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var extension = Path.GetExtension(imageFile.FileName);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await imageFile.CopyToAsync(stream);

            return $"/images/posts/{fileName}";
        }

        private void DeleteLocalImage(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/images/posts/", StringComparison.OrdinalIgnoreCase))
                return;

            var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }

        // 9. THẢ TIM BÀI VIẾT (XỬ LÝ AJAX)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleLike(int postId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var existingLike = await _context.Likes
                .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == user.Id);

            var postExists = await _context.Posts.AnyAsync(p => p.Id == postId && !p.IsRemovedByAi);
            if (!postExists)
                return Json(new { success = false, message = "Bài viết đã bị gỡ do vi phạm." });

            bool isLikedNow = false;

            if (existingLike != null)
            {
                _context.Likes.Remove(existingLike);
                isLikedNow = false;
            }
            else
            {
                var newLike = new Like { PostId = postId, UserId = user.Id };
                _context.Likes.Add(newLike);
                isLikedNow = true;
            }

            await _context.SaveChangesAsync();

            var totalLikes = await _context.Likes.CountAsync(l => l.PostId == postId);

            return Json(new { success = true, isLiked = isLikedNow, count = totalLikes });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Report(int postId, string? reason)
        {
            EnsurePostReportsTable();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == postId && !p.IsRemovedByAi);
            if (post == null) return NotFound();

            if (post.UserId == user.Id)
            {
                TempData["Error"] = "Bạn không thể báo cáo bài viết của chính mình.";
                return RedirectToAction(nameof(Details), new { id = postId });
            }

            var hasPendingReport = await _context.PostReports.AnyAsync(r =>
                r.PostId == postId &&
                r.ReporterId == user.Id &&
                r.Status == "Pending");

            if (!hasPendingReport)
            {
                _context.PostReports.Add(new PostReport
                {
                    PostId = postId,
                    ReporterId = user.Id,
                    Reason = string.IsNullOrWhiteSpace(reason) ? "Nội dung vi phạm" : reason.Trim(),
                    Status = "Pending",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã gửi báo cáo bài viết đến quản trị viên.";
            }
            else
            {
                TempData["Error"] = "Bạn đã báo cáo bài viết này và đang chờ quản trị viên xử lý.";
            }

            return RedirectToAction(nameof(Details), new { id = postId });
        }

        // 10. BÌNH LUẬN (ĐỒNG BỘ - RELOAD TRANG)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int postId, string commentContent)
        {
            if (string.IsNullOrWhiteSpace(commentContent))
            {
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var postExists = await _context.Posts.AnyAsync(p => p.Id == postId && !p.IsRemovedByAi);
            if (!postExists)
                return RedirectToAction(nameof(Index));

            var newComment = new Comment
            {
                PostId = postId,
                UserId = user.Id,
                Content = commentContent,
                CreatedAt = DateTime.Now
            };

            _context.Comments.Add(newComment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // 11. THEO DÕI / BỎ THEO DÕI (AJAX)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleFollow([FromForm] string targetUserId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Json(new { success = false, message = "Vui lòng đăng nhập." });
            if (currentUser.Id == targetUserId) return Json(new { success = false, message = "Không thể tự theo dõi bản thân." });

            var existing = await _context.Follows
                .FirstOrDefaultAsync(f => f.FollowerId == currentUser.Id && f.FollowingId == targetUserId);

            bool isFollowingNow;
            if (existing != null)
            {
                _context.Follows.Remove(existing);
                isFollowingNow = false;
            }
            else
            {
                _context.Follows.Add(new Follow { FollowerId = currentUser.Id, FollowingId = targetUserId });
                isFollowingNow = true;
            }

            await _context.SaveChangesAsync();

            var followerCount = await _context.Follows.CountAsync(f => f.FollowingId == targetUserId);
            return Json(new { success = true, isFollowing = isFollowingNow, followerCount });
        }

        // 12. THÊM BÌNH LUẬN - TRẢ VỀ JSON CHO INLINE COMMENT (XỬ LÝ AJAX)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCommentAjax(int postId, string commentContent)
        {
            if (string.IsNullOrWhiteSpace(commentContent))
                return Json(new { success = false, message = "Nội dung không được để trống." });

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Vui lòng đăng nhập." });

            var postExists = await _context.Posts.AnyAsync(p => p.Id == postId && !p.IsRemovedByAi);
            if (!postExists)
                return Json(new { success = false, message = "Bài viết đã bị gỡ do vi phạm." });

            var newComment = new Comment
            {
                PostId = postId,
                UserId = user.Id,
                Content = commentContent,
                CreatedAt = DateTime.Now
            };

            _context.Comments.Add(newComment);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                id = newComment.Id,
                content = newComment.Content,
                createdAt = newComment.CreatedAt.ToString("dd/MM HH:mm"),
                authorName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.UserName, // Ưu tiên tên hiển thị
                avatarUrl = user.AvatarUrl ?? ""
            });
        }

        private async Task LoadFollowingIdsAsync()
        {
            var currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(currentUserId))
            {
                ViewData["FollowingIds"] = new HashSet<string>();
                return;
            }

            var followingIds = await _context.Follows
                .Where(f => f.FollowerId == currentUserId)
                .Select(f => f.FollowingId)
                .ToListAsync();

            ViewData["FollowingIds"] = new HashSet<string>(followingIds);
        }

        private void EnsurePostReportsTable()
        {
            _context.Database.ExecuteSqlRaw(@"
            IF OBJECT_ID(N'[dbo].[PostReports]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[PostReports]
                (
                    [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_PostReports] PRIMARY KEY,
                    [PostId] int NOT NULL,
                    [ReporterId] nvarchar(450) NOT NULL,
                    [Reason] nvarchar(300) NOT NULL,
                    [Status] nvarchar(30) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [ReviewedAt] datetime2 NULL,
                    [ReviewedByAdminId] nvarchar(max) NULL,
                    [AdminNote] nvarchar(500) NULL,
                    CONSTRAINT [FK_PostReports_Posts_PostId]
                        FOREIGN KEY ([PostId]) REFERENCES [dbo].[Posts]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_PostReports_Users_ReporterId]
                        FOREIGN KEY ([ReporterId]) REFERENCES [dbo].[Users]([Id]) ON DELETE NO ACTION
                );

                CREATE INDEX [IX_PostReports_PostId]
                    ON [dbo].[PostReports]([PostId]);

                CREATE INDEX [IX_PostReports_ReporterId]
                    ON [dbo].[PostReports]([ReporterId]);
            END;
            ");
        }

        private async Task SaveRejectedPostForAdminReviewAsync(Post post, AppUser author, ContentModerationResult moderation)
        {
            EnsurePostReportsTable();

            var reason = string.IsNullOrWhiteSpace(moderation.Reason)
                ? "AI phát hiện nội dung có dấu hiệu vi phạm hoặc spam."
                : $"AI phát hiện: {moderation.Reason}";

            var trimmedReason = reason.Length > 300 ? reason[..300] : reason;
            post.UserId = author.Id;
            post.CreatedAt = DateTime.Now;
            post.IsRemovedByAi = true;
            post.ViolationReason = trimmedReason;
            post.RemovedAt = DateTime.Now;
            post.User = author;

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            _context.PostReports.Add(new PostReport
            {
                PostId = post.Id,
                ReporterId = author.Id,
                Reason = trimmedReason,
                Status = "Pending",
                CreatedAt = DateTime.Now
            });

            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var admin in admins)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = admin.Id,
                    Title = "AI phát hiện nội dung vi phạm",
                    Content = $"Bài viết #{post.Id} của {author.FullName ?? author.UserName} bị chặn khi đăng. Lý do: {trimmedReason}",
                    CreatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }
    }
}
