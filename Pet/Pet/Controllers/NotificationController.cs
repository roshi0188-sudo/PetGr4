using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetSocial.Data;
using System.Security.Claims;

namespace PetSocial.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var notifications = _context.Notifications
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            return View(notifications);
        }

        public IActionResult MarkAllRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var notifications = _context.Notifications
                .Where(x => x.UserId == userId && !x.IsRead)
                .ToList();

            foreach (var item in notifications)
            {
                item.IsRead = true;
            }

            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Open(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var notification = _context.Notifications
                .FirstOrDefault(x => x.Id == id && x.UserId == userId);

            if (notification == null)
                return RedirectToAction(nameof(Index));

            notification.IsRead = true;
            _context.SaveChanges();

            var isViolationNotice =
                notification.Title.Contains("vi phạm", StringComparison.OrdinalIgnoreCase) ||
                notification.Content.Contains("vi phạm", StringComparison.OrdinalIgnoreCase) ||
                notification.Content.Contains("AI", StringComparison.OrdinalIgnoreCase);

            if (User.IsInRole("Admin") && isViolationNotice)
            {
                return RedirectToAction(
                    "Reports",
                    "Posts",
                    new { area = "Admin", status = "Pending" });
            }

            return RedirectToAction(nameof(Index), null, $"notification-{notification.Id}");
        }
    }
}
