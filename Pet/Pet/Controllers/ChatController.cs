using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using System.Security.Claims;

namespace PetSocial.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ChatController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Danh sách cuộc trò chuyện
        public IActionResult Conversations()
        {
            var currentUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            var users = _context.Messages
                .Include(x => x.Sender)
                .Include(x => x.Receiver)
                .Where(x =>
                    x.SenderId == currentUserId
                    || x.ReceiverId == currentUserId)
                .Select(x =>
                    x.SenderId == currentUserId
                        ? x.Receiver
                        : x.Sender)
                .Distinct()
                .ToList();

            return View(users);
        }

        // Trang chat
        public IActionResult Index(string userId)
        {
            var currentUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            // Danh sách người đã chat
            var chatUsers = _context.Messages
                .Include(x => x.Sender)
                .Include(x => x.Receiver)
                .Where(x =>
                    x.SenderId == currentUserId
                    || x.ReceiverId == currentUserId)
                .Select(x =>
                    x.SenderId == currentUserId
                        ? x.Receiver
                        : x.Sender)
                .Distinct()
                .ToList();

            ViewBag.ChatUsers = chatUsers;

            if (string.IsNullOrEmpty(userId))
            {
                ViewBag.CurrentUserId = currentUserId;
                return View(new List<Models.Message>());
            }

            var receiver =
                _context.Users
                .FirstOrDefault(x => x.Id == userId);

            if (receiver == null)
            {
                return NotFound();
            }

            var messages =
                _context.Messages
                .Include(x => x.Sender)
                .Include(x => x.Receiver)
                .Where(x =>
                    (x.SenderId == currentUserId &&
                     x.ReceiverId == userId)
                    ||
                    (x.SenderId == userId &&
                     x.ReceiverId == currentUserId))
                .OrderBy(x => x.CreatedAt)
                .ToList();

            ViewBag.Receiver = receiver;
            ViewBag.ReceiverId = receiver.Id;
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.ChatUsers = chatUsers;

            return View(messages);
        }
    }
}