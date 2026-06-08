using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using System.Security.Claims;

namespace PetSocial.Controllers
{
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            var notifications = _context.Notifications
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            return View(notifications);
        }

        public IActionResult MarkAllRead()
        {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            var notifications =
                _context.Notifications
                .Where(x =>
                    x.UserId == userId &&
                    !x.IsRead)
                .ToList();

            foreach (var item in notifications)
            {
                item.IsRead = true;
            }

            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }
    }
}