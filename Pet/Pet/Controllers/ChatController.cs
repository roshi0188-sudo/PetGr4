using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.Models;
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

        public IActionResult Index(string? userId)
        {
            var currentUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                var unreadMessages = _context.Messages
                    .Where(x =>
                        x.SenderId == userId &&
                        x.ReceiverId == currentUserId &&
                        !x.IsRead)
                    .ToList();

                if (unreadMessages.Any())
                {
                    foreach (var message in unreadMessages)
                    {
                        message.IsRead = true;
                    }

                    _context.SaveChanges();
                }
            }

            var conversationMessages = _context.Messages
                .Include(x => x.Sender)
                .Include(x => x.Receiver)
                .Where(x =>
                    x.SenderId == currentUserId ||
                    x.ReceiverId == currentUserId)
                .ToList();

            // Danh sách người đã chat (không trùng)
            var chatUsers = conversationMessages
                .GroupBy(x => x.SenderId == currentUserId ? x.ReceiverId : x.SenderId)
                .Select(g =>
                    g.OrderByDescending(x => x.CreatedAt).First().SenderId == currentUserId
                        ? g.OrderByDescending(x => x.CreatedAt).First().Receiver
                        : g.OrderByDescending(x => x.CreatedAt).First().Sender)
                .OrderByDescending(x =>
                    conversationMessages
                        .Where(m =>
                            (m.SenderId == currentUserId && m.ReceiverId == x.Id) ||
                            (m.SenderId == x.Id && m.ReceiverId == currentUserId))
                        .Max(m => m.CreatedAt))
                .ToList();

            ViewBag.UnreadCounts = conversationMessages
                .Where(x => x.ReceiverId == currentUserId && !x.IsRead)
                .GroupBy(x => x.SenderId)
                .ToDictionary(g => g.Key, g => g.Count());

            ViewBag.LastMessages = conversationMessages
                .GroupBy(x => x.SenderId == currentUserId ? x.ReceiverId : x.SenderId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.CreatedAt).First().Content);

            ViewBag.LastMessageTimes = conversationMessages
                .GroupBy(x => x.SenderId == currentUserId ? x.ReceiverId : x.SenderId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.CreatedAt).First().CreatedAt);

            ViewBag.ChatUsers = chatUsers;

            ViewBag.CurrentUserId = currentUserId;

            // Nếu chưa chọn ai
            if (string.IsNullOrEmpty(userId))
            {
                return View(new List<Message>());
            }

            var receiver = _context.Users
                .FirstOrDefault(x => x.Id == userId);

            if (receiver == null)
                return NotFound();

            var messages = _context.Messages
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

            return View(messages);
        }
    }
}
