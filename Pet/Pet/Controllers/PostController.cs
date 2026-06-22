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
                    .ThenInclude(c => c.User)
                .Include(p => p.Likes)
                .Where(p => !p.IsRemovedByAi)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            await RemoveViolatingExistingCommentsAsync(posts);
            await LoadFollowingIdsAsync();

            ViewData["ActiveMenu"] = "Home";
            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            ViewBag.IsAdmin = User.IsInRole("Admin");

            return View(posts);
        }

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
                    .ThenInclude(c => c.User)
                .Include(p => p.Likes)
                .Where(p => followingIds.Contains(p.UserId) && !p.IsRemovedByAi)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            await RemoveViolatingExistingCommentsAsync(posts);
            await LoadFollowingIdsAsync();

            ViewData["IsFeed"] = true;
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
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();

            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            ViewBag.IsAdmin = User.IsInRole("Admin");
            if (post.IsRemovedByAi && post.UserId != ViewBag.CurrentUserId && !ViewBag.IsAdmin)
                return NotFound();

            await RemoveViolatingExistingCommentsAsync(new[] { post });

            return View(post);
        }

        // 3. HIỂN THỊ FORM TẠO BÀI VIẾT MỚI
        [Authorize]
        public async Task<IActionResult> Create(string? returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User);
            var model = new Post();
            if (user != null)
            {
                model.User = user;
                model.UserId = user.Id;
            }
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        // 4. XỬ LÝ TẠO BÀI VIẾT MỚI
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Post model, IFormFile? imageFile, string? returnUrl = null)
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

            var moderation = await _petAiService.CheckContentAsync(model.Content, imageFile);
            if (IsViolation(moderation))
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    var imageUrl = await SavePostImageAsync(imageFile);
                    if (!string.IsNullOrWhiteSpace(imageUrl)) model.ImageUrl = imageUrl;
                }

                var reason = BuildModerationReason(moderation);
                model.IsRemovedByAi = true;
                model.ViolationReason = reason;
                model.RemovedAt = DateTime.Now;

                _context.Posts.Add(model);
                await _context.SaveChangesAsync();
                await CreateAutomaticReportIfNeededAsync(model, user.Id, reason);
                await NotifyPostViolationAsync(model, user, reason, isNewPost: true);

                TempData["Error"] = BuildModerationMessage(moderation);
                return RedirectToAction(nameof(Index));
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                var imageUrl = await SavePostImageAsync(imageFile);
                if (!string.IsNullOrWhiteSpace(imageUrl)) model.ImageUrl = imageUrl;
            }

            _context.Posts.Add(model);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index)); // Chuyển về trang chủ
        }

        // 5. HIỂN THỊ FORM CẬP NHẬT BÀI VIẾT
        [Authorize]
        public async Task<IActionResult> Edit(int id, string? returnUrl = null)
        {
            var post = await _context.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();

            if (!await CanManagePostAsync(post)) return Forbid();

            ViewBag.ReturnUrl = returnUrl;
            return View(post);
        }

        // 6. XỬ LÝ CẬP NHẬT BÀI VIẾT
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Post formModel, IFormFile? imageFile, string? returnUrl = null)
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

            var moderation = await _petAiService.CheckContentAsync(formModel.Content, imageFile);
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

            if (IsViolation(moderation))
            {
                var reason = BuildModerationReason(moderation);
                post.IsRemovedByAi = true;
                post.ViolationReason = reason;
                post.RemovedAt = DateTime.Now;

                _context.Update(post);
                await _context.SaveChangesAsync();
                await CreateAutomaticReportIfNeededAsync(post, post.UserId, reason);

                var owner = await _userManager.FindByIdAsync(post.UserId);
                if (owner != null)
                    await NotifyPostViolationAsync(post, owner, reason, isNewPost: false);

                TempData["Error"] = BuildModerationMessage(moderation);
                return RedirectToAction(nameof(Index));
            }

            _context.Update(post);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index)); // Chuyển về trang chủ
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Report(int postId, string? reason, string? returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var post = await _context.Posts.FindAsync(postId);
            if (post == null) return NotFound();
            if (post.UserId == user.Id)
            {
                TempData["Error"] = "Không thể báo cáo bài viết của chính bạn.";
                return RedirectToLocalOrIndex(returnUrl);
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
                TempData["Error"] = "Bạn đã báo cáo bài viết này, vui lòng chờ quản trị viên xử lý.";
            }

            return RedirectToLocalOrIndex(returnUrl);
        }

        // 7. HIỂN THỊ FORM XÁC NHẬN XÓA BÀI VIẾT
        [Authorize]
        public async Task<IActionResult> Delete(int id, string? returnUrl = null)
        {
            var post = await _context.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();
            if (!await CanManagePostAsync(post)) return Forbid();

            ViewBag.ReturnUrl = returnUrl;
            return View(post);
        }

        // 8. XỬ LÝ XOÁ BÀI VIẾT
        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string? returnUrl = null)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null) return NotFound();
            if (!await CanManagePostAsync(post)) return Forbid();

            DeleteLocalImage(post.ImageUrl);

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index)); // Chuyển về trang chủ
        }

        // ... (Giữ nguyên các phương thức: CanManagePostAsync, SavePostImageAsync, DeleteLocalImage, ToggleLike, AddComment, ToggleFollow, AddCommentAjax, LoadFollowingIdsAsync)

        private async Task<bool> CanManagePostAsync(Post post)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;
            return post.UserId == user.Id || User.IsInRole("Admin");
        }

        private static bool IsViolation(ContentModerationResult moderation)
        {
            return moderation.IsFlagged || moderation.IsSpam;
        }

        private static string BuildModerationMessage(ContentModerationResult moderation)
        {
            return "AI đã tự gỡ nội dung vi phạm tiêu chuẩn cộng đồng và gửi thông báo đến bạn cùng quản trị viên.";
        }

        private static string BuildModerationReason(ContentModerationResult moderation)
        {
            return string.IsNullOrWhiteSpace(moderation.Reason)
                ? "Nội dung có dấu hiệu vi phạm tiêu chuẩn cộng đồng."
                : moderation.Reason.Trim();
        }

        private async Task HidePostForViolationAsync(Post post, string reason)
        {
            post.IsRemovedByAi = true;
            post.ViolationReason = reason;
            post.RemovedAt = DateTime.Now;
            _context.Posts.Update(post);
            await _context.SaveChangesAsync();
        }

        private async Task CreateAutomaticReportIfNeededAsync(Post post, string reporterId, string reason)
        {
            var hasPendingAutoReport = await _context.PostReports.AnyAsync(r =>
                r.PostId == post.Id &&
                r.Status == "Pending" &&
                r.Reason.StartsWith("[AI]"));

            if (hasPendingAutoReport) return;

            _context.PostReports.Add(new PostReport
            {
                PostId = post.Id,
                ReporterId = reporterId,
                Reason = Truncate("[AI] " + reason, 300),
                Status = "Pending",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }

        private async Task NotifyPostViolationAsync(Post post, AppUser owner, string reason, bool isNewPost)
        {
            var actionText = isNewPost ? "bài viết mới" : "bài viết đã chỉnh sửa";
            _context.Notifications.Add(new Notification
            {
                UserId = owner.Id,
                Title = "AI đã gỡ bài viết vi phạm",
                Content = $"AI đã tự gỡ {actionText} của bạn khỏi cộng đồng. Lý do: {reason}",
                CreatedAt = DateTime.Now
            });

            await NotifyAdminsAsync(
                "AI phát hiện nội dung vi phạm",
                $"AI đã tự gỡ bài viết #{post.Id} của {(string.IsNullOrWhiteSpace(owner.FullName) ? owner.UserName : owner.FullName)}. Lý do: {reason}",
                owner.Id);

            await _context.SaveChangesAsync();
        }

        private async Task NotifyCommentViolationAsync(AppUser commenter, Post post, string reason)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = commenter.Id,
                Title = "AI đã chặn bình luận vi phạm",
                Content = $"Bình luận của bạn trong bài viết #{post.Id} không được đăng vì vi phạm tiêu chuẩn cộng đồng. Lý do: {reason}",
                CreatedAt = DateTime.Now
            });

            await NotifyAdminsAsync(
                "AI phát hiện bình luận vi phạm",
                $"AI đã chặn bình luận của {(string.IsNullOrWhiteSpace(commenter.FullName) ? commenter.UserName : commenter.FullName)} trong bài viết #{post.Id}. Lý do: {reason}",
                commenter.Id);

            await _context.SaveChangesAsync();
        }

        private async Task RemoveViolatingExistingCommentsAsync(IEnumerable<Post> posts)
        {
            foreach (var post in posts)
            {
                foreach (var comment in post.Comments.ToList())
                {
                    if (comment.User == null || string.IsNullOrWhiteSpace(comment.Content))
                        continue;

                    var moderation = await _petAiService.CheckContentAsync(comment.Content);
                    if (!IsViolation(moderation))
                        continue;

                    var reason = BuildModerationReason(moderation);
                    _context.Comments.Remove(comment);
                    post.Comments.Remove(comment);
                    await NotifyCommentViolationAsync(comment.User, post, reason);
                }
            }
        }

        private async Task NotifyAdminsAsync(string title, string content, string? excludeUserId = null)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var admin in admins.Where(a => a.Id != excludeUserId))
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = admin.Id,
                    Title = title,
                    Content = content,
                    CreatedAt = DateTime.Now
                });
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
                return value;

            return value[..maxLength];
        }

        private IActionResult RedirectToLocalOrIndex(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }

        private async Task<string?> SavePostImageAsync(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) return null;
            if (string.IsNullOrWhiteSpace(imageFile.ContentType) || !imageFile.ContentType.StartsWith("image/"))
                return null;
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
            if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/images/posts/", StringComparison.OrdinalIgnoreCase))
                return;

            var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

            if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleLike(int postId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var post = await _context.Posts.FindAsync(postId);
            if (post == null || post.IsRemovedByAi)
                return Json(new { success = false, message = "Bài viết không còn khả dụng." });

            var existingLike = await _context.Likes.FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == user.Id);
            bool isLikedNow = false;

            if (existingLike != null)
            {
                _context.Likes.Remove(existingLike);
                isLikedNow = false;
            }
            else
            {
                _context.Likes.Add(new Like { PostId = postId, UserId = user.Id });
                isLikedNow = true;
            }
            await _context.SaveChangesAsync();
            var totalLikes = await _context.Likes.CountAsync(l => l.PostId == postId);
            return Json(new
            {
                success = true,
                isLiked = isLikedNow,
                count = totalLikes,
                userId = user.Id,
                userName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.UserName,
                avatarUrl = user.AvatarUrl ?? "",
                createdAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
            });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int postId, string commentContent, string? returnUrl = null)
        {
            if (!string.IsNullOrWhiteSpace(commentContent))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();

                var post = await _context.Posts.FindAsync(postId);
                if (post == null || post.IsRemovedByAi)
                    return NotFound();

                var moderation = await _petAiService.CheckContentAsync(commentContent);
                if (IsViolation(moderation))
                {
                    await NotifyCommentViolationAsync(user, post, BuildModerationReason(moderation));
                    TempData["Error"] = BuildModerationMessage(moderation);
                    return RedirectToLocalOrIndex(returnUrl);
                }

                _context.Comments.Add(new Comment { PostId = postId, UserId = user.Id, Content = commentContent, CreatedAt = DateTime.Now });
                await _context.SaveChangesAsync();
            }

            return RedirectToLocalOrIndex(returnUrl);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleFollow([FromForm] string targetUserId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Json(new { success = false, message = "Vui lòng đăng nhập." });
            if (currentUser.Id == targetUserId) return Json(new { success = false, message = "Không thể tự theo dõi bản thân." });

            var existing = await _context.Follows.FirstOrDefaultAsync(f => f.FollowerId == currentUser.Id && f.FollowingId == targetUserId);
            bool isFollowingNow;
            if (existing != null) { _context.Follows.Remove(existing); isFollowingNow = false; }
            else { _context.Follows.Add(new Follow { FollowerId = currentUser.Id, FollowingId = targetUserId }); isFollowingNow = true; }

            await _context.SaveChangesAsync();
            return Json(new { success = true, isFollowing = isFollowingNow, followerCount = await _context.Follows.CountAsync(f => f.FollowingId == targetUserId) });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCommentAjax(int postId, string commentContent)
        {
            if (string.IsNullOrWhiteSpace(commentContent)) return Json(new { success = false, message = "Nội dung trống." });
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Vui lòng đăng nhập." });

            var post = await _context.Posts.FindAsync(postId);
            if (post == null || post.IsRemovedByAi)
                return Json(new { success = false, message = "Bài viết không còn khả dụng." });

            var moderation = await _petAiService.CheckContentAsync(commentContent);
            if (IsViolation(moderation))
            {
                await NotifyCommentViolationAsync(user, post, BuildModerationReason(moderation));
                return Json(new { success = false, message = BuildModerationMessage(moderation) });
            }

            var comment = new Comment { PostId = postId, UserId = user.Id, Content = commentContent, CreatedAt = DateTime.Now };
            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();
            return Json(new
            {
                success = true,
                id = comment.Id,
                content = comment.Content,
                createdAt = comment.CreatedAt.ToString("dd/MM HH:mm"),
                authorName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.UserName, // Ưu tiên tên hiển thị
                userId = user.Id,
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
