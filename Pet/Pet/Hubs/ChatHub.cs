using Microsoft.AspNetCore.SignalR;
using PetSocial.Data;
using PetSocial.Models;
using System.Security.Claims;

namespace PetSocial.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;

        public ChatHub(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SendMessage(
            string receiverId,
            string content)
        {
            var senderId = Context.User?
                .FindFirst(ClaimTypes.NameIdentifier)?
                .Value;

            if (string.IsNullOrEmpty(senderId))
                return;

            var senderUser = _context.Users
                .FirstOrDefault(x => x.Id == senderId);

            if (senderUser == null)
                return;

            // Lưu tin nhắn
            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Messages.Add(message);

            // Lưu notification
            var notification = new Notification
            {
                UserId = receiverId,
                Title = "Tin nhắn mới",
                Content = $"{senderUser.FullName}: {content}",
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            string timeText =
                message.CreatedAt.ToString("HH:mm");

            // Tin nhắn realtime
            await Clients.User(receiverId)
                .SendAsync(
                    "ReceiveMessage",
                    senderId,
                    senderUser.FullName,
                    content,
                    timeText);

            await Clients.Caller
                .SendAsync(
                    "ReceiveMessage",
                    senderId,
                    senderUser.FullName,
                    content,
                    timeText);

            // Notification realtime
            await Clients.User(receiverId)
                .SendAsync(
                    "ReceiveNotification",
                    notification.Title,
                    notification.Content,
                    timeText);
        }
    }
}