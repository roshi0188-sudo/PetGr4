using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.Models;
using PetSocial.ViewModels;
using System.Security.Claims;

namespace PetSocial.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PostsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public PostsController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<IActionResult> Index(string? search)
        {
            EnsurePostReportsTable();

            var postsQuery = _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                .Include(p => p.Likes)
                .Include(p => p.Reports)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                postsQuery = postsQuery.Where(p =>
                    p.Content.Contains(keyword) ||
                    p.User.FullName.Contains(keyword) ||
                    (p.User.Email != null && p.User.Email.Contains(keyword)));
            }

            var today = DateTime.Today;
            var chartStart = today.AddDays(-6);
            var postsByDay = await _context.Posts
                .Where(p => p.CreatedAt.Date >= chartStart)
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            var labels = new List<string>();
            var values = new List<int>();

            for (var day = chartStart; day <= today; day = day.AddDays(1))
            {
                labels.Add(day.ToString("dd/MM"));
                values.Add(postsByDay.FirstOrDefault(x => x.Date == day)?.Count ?? 0);
            }

            var model = new AdminPostManagementVM
            {
                Search = search,
                TotalPosts = await _context.Posts.CountAsync(),
                TotalReports = await _context.PostReports.CountAsync(),
                PendingReports = await _context.PostReports.CountAsync(r => r.Status == "Pending"),
                ReportedPosts = await _context.PostReports.Select(r => r.PostId).Distinct().CountAsync(),
                Posts = await postsQuery
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync(),
                ChartLabels = labels,
                ChartValues = values
            };

            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            EnsurePostReportsTable();

            var post = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments).ThenInclude(c => c.User)
                .Include(p => p.Likes)
                .Include(p => p.Reports).ThenInclude(r => r.Reporter)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
                return NotFound();

            return View(post);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteViolation(int id)
        {
            EnsurePostReportsTable();

            var post = await _context.Posts.FindAsync(id);
            if (post == null)
                return NotFound();

            DeleteLocalImage(post.ImageUrl);

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa bài viết vi phạm.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HideViolation(int id, string? reason, string? returnTo = null)
        {
            EnsurePostReportsTable();

            var post = await _context.Posts.FindAsync(id);
            if (post == null)
                return NotFound();

            post.IsRemovedByAi = true;
            post.ViolationReason = string.IsNullOrWhiteSpace(reason)
                ? "Quản trị viên đã gỡ bài viết vì vi phạm tiêu chuẩn cộng đồng."
                : reason.Trim();
            post.RemovedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã gỡ bài viết khỏi cộng đồng.";
            return RedirectBackToModerationPage(returnTo, id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestorePost(int id, string? returnTo = null)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null)
                return NotFound();

            post.IsRemovedByAi = false;
            post.ViolationReason = null;
            post.RemovedAt = null;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã khôi phục bài viết lên cộng đồng.";
            return RedirectBackToModerationPage(returnTo, id);
        }

        public async Task<IActionResult> Reports(string status = "Pending")
        {
            EnsurePostReportsTable();

            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "Pending" : status.Trim();

            var reports = await _context.PostReports
                .Include(r => r.Post).ThenInclude(p => p.User)
                .Include(r => r.Reporter)
                .Where(r => r.Status == normalizedStatus)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            if (normalizedStatus == "Pending" && reports.Count == 0)
            {
                reports = await _context.PostReports
                    .Include(r => r.Post).ThenInclude(p => p.User)
                    .Include(r => r.Reporter)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                normalizedStatus = "All";
            }

            var model = new AdminPostReportsVM
            {
                Status = normalizedStatus,
                PendingCount = await _context.PostReports.CountAsync(r => r.Status == "Pending"),
                ResolvedCount = await _context.PostReports.CountAsync(r => r.Status == "Resolved"),
                DismissedCount = await _context.PostReports.CountAsync(r => r.Status == "Dismissed"),
                Reports = reports
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReportStatus(int id, string status, string? adminNote)
        {
            EnsurePostReportsTable();

            if (status != "Resolved" && status != "Dismissed")
                return BadRequest();

            var report = await _context.PostReports
                .Include(r => r.Post)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (report == null)
                return NotFound();

            report.Status = status;
            report.AdminNote = adminNote;
            report.ReviewedAt = DateTime.Now;
            report.ReviewedByAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (status == "Resolved" && report.Post != null)
            {
                report.Post.IsRemovedByAi = true;
                report.Post.ViolationReason = string.IsNullOrWhiteSpace(adminNote)
                    ? report.Reason
                    : adminNote.Trim();
                report.Post.RemovedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = status == "Resolved"
                ? "Đã đánh dấu báo cáo là đã xử lý."
                : "Đã bỏ qua báo cáo.";

            return RedirectToAction(nameof(Reports), new { status = "Pending" });
        }

        private IActionResult RedirectBackToModerationPage(string? returnTo, int postId)
        {
            if (string.Equals(returnTo, "Details", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(Details), new { id = postId });

            return RedirectToAction(nameof(Reports), new { status = "Pending" });
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

        private void DeleteLocalImage(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) ||
                !imageUrl.StartsWith("/images/posts/", StringComparison.OrdinalIgnoreCase))
                return;

            var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
    }
}
