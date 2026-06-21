using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetSocial.Data;
using PetSocial.Models;
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

            return GetNotificationRedirect(notification);
        }

        private IActionResult GetNotificationRedirect(Notification notification)
        {
            var title = notification.Title ?? string.Empty;
            var content = notification.Content ?? string.Empty;
            var text = $"{title} {content}";

            if (ContainsAny(text, "Tin nh", "Message"))
            {
                return RedirectToAction("Index", "Chat");
            }

            if (ContainsAny(text, "Gh", "thanh cong", "thành công"))
            {
                return RedirectToAction("Matches", "Match");
            }

            if (ContainsAny(text, "ket noi", "kết nối", "Match"))
            {
                return RedirectToAction("Requests", "Match");
            }

            return RedirectToAction(nameof(Index), null, $"notification-{notification.Id}");
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            return values.Any(value =>
                text.Contains(value, StringComparison.OrdinalIgnoreCase));
        }
    }
}
