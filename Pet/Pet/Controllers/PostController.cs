using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.Models;
using System;
using System.IO;
using System.Threading.Tasks;

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

        // 1. HIỂN THỊ DANH SÁCH BÀI VIẾT (BẢNG TIN CHÍNH)
        public async Task<IActionResult> Index()
        {
            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                .Include(p => p.Likes)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return View(posts);
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

            return View(post);
        }



        // 3. HIỂN THỊ FORM TẠO BÀI VIẾT MỚI
        [Authorize]
        public IActionResult Create() => View(new Post());

        // 4. XỬ LÝ TẠO BÀI VIẾT MỚI
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Post model, IFormFile? imageFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Gán dữ liệu hệ thống trước khi kiểm tra hợp lệ
            model.UserId = user.Id;
            model.CreatedAt = DateTime.Now;

            // Loại bỏ kiểm tra thực thể liên kết để ModelState không báo lỗi oan
            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("ImageUrl");

            if (!ModelState.IsValid) return View(model);

            // Xử lý lưu ảnh vào thư mục wwwroot/images/posts
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

            // SỬA LỖI: Quay về trang Bảng tin chính (Index) sau khi đăng thành công
            return RedirectToAction(nameof(Index));
        }

        // 5. HIỂN THỊ FORM CẬP NHẬT BÀI VIẾT
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var post = await _context.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
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

            var post = await _context.Posts.FindAsync(id);
            if (post == null) return NotFound();
            if (!await CanManagePostAsync(post)) return Forbid();

            // Loại bỏ kiểm tra ràng buộc không cần thiết
            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("ImageUrl");

            if (!ModelState.IsValid)
            {
                return View(formModel);
            }

            // Cập nhật nội dung văn bản
            post.Content = formModel.Content ?? string.Empty;

            // Xử lý hình ảnh giống như ProductController tham khảo
            if (imageFile != null && imageFile.Length > 0)
            {
                // Lưu hình ảnh mới
                var newImageUrl = await SavePostImageAsync(imageFile);
                if (!string.IsNullOrWhiteSpace(newImageUrl))
                {
                    // Xóa ảnh cũ trên server để tránh rác bộ nhớ dữ liệu
                    DeleteLocalImage(post.ImageUrl);
                    post.ImageUrl = newImageUrl;
                }
            }
            // Nếu không chọn ảnh mới (imageFile == null), post.ImageUrl sẽ giữ nguyên ảnh cũ giống code tham khảo

            _context.Update(post);
            await _context.SaveChangesAsync();

            // Điều hướng về danh sách Bảng tin (Index) sau khi chỉnh sửa thành công
            return RedirectToAction(nameof(Index));
        }

        // 7. HIỂN THỊ FORM XÁC NHẬN XÓA BÀI VIẾT
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
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
            var post = await _context.Posts.FindAsync(id);
            if (post == null) return NotFound();
            if (!await CanManagePostAsync(post)) return Forbid();

            // Xóa file ảnh vật lý trong thư mục wwwroot
            DeleteLocalImage(post.ImageUrl);

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // PHÂN QUYỀN: KIỂM TRA CHỦ SỞ HỮU BÀI VIẾT HOẶC ADMIN
        private async Task<bool> CanManagePostAsync(Post post)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;

            return post.UserId == user.Id || User.IsInRole("Admin");
        }

        // HÀM LƯU ẢNH CHUẨN VÀO WWWROOT/IMAGES/POSTS
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

            // Tạo tên file duy nhất bằng Guid để không bị đè file trùng tên
            var extension = Path.GetExtension(imageFile.FileName);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await imageFile.CopyToAsync(stream);

            // Trả về đường dẫn tương đối để lưu vào DB
            return $"/images/posts/{fileName}";
        }

        // HÀM XÓA FILE ẢNH VẬT LÝ KHỎI SERVER KHI SỬA/XÓA
        private void DeleteLocalImage(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/images/posts/", StringComparison.OrdinalIgnoreCase))
                return;

            var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }

        // 9. THẢ TIM BÀI VIẾT
        // 9. THẢ TIM BÀI VIẾT (XỬ LÝ ASYNC QUA AJAX)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleLike(int postId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var existingLike = await _context.Likes
                .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == user.Id);

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

            // Tính lại tổng số lượt thích hiện tại của bài viết
            var totalLikes = await _context.Likes.CountAsync(l => l.PostId == postId);

            // Trả về dữ liệu JSON để JavaScript xử lý giao diện trực tiếp
            return Json(new { success = true, isLiked = isLikedNow, count = totalLikes });
        }

        // 10. BÌNH LUẬN
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
    }
}