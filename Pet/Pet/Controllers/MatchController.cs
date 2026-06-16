using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.Hubs;
using PetSocial.Models;
using System.Security.Claims;

namespace PetSocial.Controllers
{
    public class MatchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;

        public MatchController(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public IActionResult Index()
        {
            var pets = _context.Pets.ToList();

            return View(pets);
        }

        public IActionResult Suggest(int id)
        {
            var pet = _context.Pets.FirstOrDefault(x => x.Id == id);
            if (pet == null) return NotFound();

            ViewBag.PetName = pet.Name;

            var suggestions = _context.Pets
                .Where(x => x.Id != pet.Id)
                .ToList();

            ViewBag.Count = suggestions.Count;

            return View(suggestions);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SendMatch(int receiverPetId)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (currentUserId == null)
                return RedirectToAction("Login", "Account");

            var senderPet = _context.Pets
                .FirstOrDefault(x => x.UserId == currentUserId);

            if (senderPet == null)
                return RedirectToAction("MyPets", "Pet");

            var receiverPet = _context.Pets
                .FirstOrDefault(x => x.Id == receiverPetId);

            if (receiverPet == null)
                return NotFound();

            if (receiverPet.UserId == currentUserId)
                return RedirectToAction("MyPets", "Pet");

            bool existed = _context.PetMatches.Any(x =>
                x.SenderPetId == senderPet.Id &&
                x.ReceiverPetId == receiverPetId);

            if (!existed)
            {
                var match = new PetMatch
                {
                    SenderPetId = senderPet.Id,
                    ReceiverPetId = receiverPetId,
                    Status = "Pending"
                };

                _context.PetMatches.Add(match);

                var notification = new Notification
                {
                    UserId = receiverPet.UserId,
                    Title = "Lời mời kết nối mới",
                    Content = $"{senderPet.Name} muốn kết nối với thú cưng của bạn",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };

                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();

                await _hubContext
                    .Clients
                    .User(receiverPet.UserId)
                    .SendAsync(
                        "ReceiveNotification",
                        notification.Title,
                        notification.Content,
                        notification.CreatedAt.ToString("HH:mm"));
            }

            return RedirectToAction("Requests");
        }

        public IActionResult Requests()
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var myPet = _context.Pets
                .FirstOrDefault(x => x.UserId == currentUserId);

            if (myPet == null)
                return RedirectToAction("Index");

            var requests = _context.PetMatches
                .Include(x => x.SenderPet)
                .Where(x =>
                    x.ReceiverPetId == myPet.Id &&
                    x.Status == "Pending")
                .ToList();

            return View(requests);
        }

        public async Task<IActionResult> Accept(int id)
        {
            var match = _context.PetMatches
                .Include(x => x.SenderPet)
                .Include(x => x.ReceiverPet)
                .FirstOrDefault(x => x.Id == id);

            if (match == null)
                return NotFound();

            match.Status = "Accepted";

            _context.Notifications.Add(
                new Notification
                {
                    UserId = match.SenderPet.UserId,
                    Title = "Lời mời được chấp nhận",
                    Content = $"{match.ReceiverPet.Name} đã chấp nhận kết nối",
                    CreatedAt = DateTime.Now
                });

            await _context.SaveChangesAsync();

            await _hubContext
                .Clients
                .User(match.SenderPet.UserId)
                .SendAsync(
                    "ReceiveNotification",
                    "Lời mời được chấp nhận",
                    $"{match.ReceiverPet.Name} đã chấp nhận kết nối",
                    DateTime.Now.ToString("HH:mm"));

            return RedirectToAction("Requests");
        }

        public IActionResult Matches()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var myPet = _context.Pets
                .FirstOrDefault(x => x.UserId == currentUserId);

            if (myPet == null)
                return RedirectToAction("MyPets", "Pet");

            ViewBag.CurrentUserId = currentUserId;

            var matches = _context.PetMatches
                .Include(x => x.SenderPet)
                .Include(x => x.ReceiverPet)
                .Where(x =>
                    (x.SenderPetId == myPet.Id ||
                     x.ReceiverPetId == myPet.Id)
                    &&
                    x.Status == "Accepted")
                .ToList();

            return View(matches);
        }
    }
}
