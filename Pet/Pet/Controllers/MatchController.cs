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

        public IActionResult Index(int? communityPetId, int? myPetId, bool showSuggestions = false)
        {
            EnsurePetMatchesTable();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var senderPetIds = _context.PetMatches
                .AsNoTracking()
                .Where(x => x.Status == "Pending" || x.Status == "Accepted")
                .Select(x => x.SenderPetId)
                .ToList();

            var receiverPetIds = _context.PetMatches
                .AsNoTracking()
                .Where(x => x.Status == "Pending" || x.Status == "Accepted")
                .Select(x => x.ReceiverPetId)
                .ToList();

            var connectedPetIds = senderPetIds
                .Concat(receiverPetIds)
                .Distinct()
                .ToList();

            var communityPet = communityPetId.HasValue
                ? _context.Pets
                    .AsNoTracking()
                    .FirstOrDefault(x => x.Id == communityPetId.Value && x.UserId != currentUserId)
                : null;

            var myPets = new List<PetModule>();

            if (!string.IsNullOrEmpty(currentUserId))
            {
                var myPetsQuery = _context.Pets
                    .AsNoTracking()
                    .Where(x => x.UserId == currentUserId)
                    .Where(x => !connectedPetIds.Contains(x.Id));

                if (communityPet != null)
                    myPetsQuery = myPetsQuery.Where(x => x.Species == communityPet.Species);

                myPets = myPetsQuery
                    .OrderBy(x => x.Name)
                    .ToList();
            }

            var selectedMyPet = myPetId.HasValue
                ? myPets.FirstOrDefault(x => x.Id == myPetId.Value)
                : null;

            var pets = new List<PetModule>();

            showSuggestions = showSuggestions || (selectedMyPet != null && communityPet == null);

            if (showSuggestions && selectedMyPet != null && communityPet == null)
            {
                pets = _context.Pets
                    .AsNoTracking()
                    .Where(x => x.UserId != currentUserId)
                    .Where(x => x.Species == selectedMyPet.Species)
                    .Where(x => !connectedPetIds.Contains(x.Id))
                    .OrderByDescending(x => x.Id)
                    .ToList();
            }

            ViewBag.MyPets = myPets;
            ViewBag.SelectedMyPet = selectedMyPet;
            ViewBag.SelectedCommunityPet = communityPet;
            ViewBag.ShowSuggestions = showSuggestions;
            ViewBag.TotalSuggestions = pets.Count;

            return View(pets);
        }

        public IActionResult Suggest(int id)
        {
            EnsurePetMatchesTable();

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
        public async Task<IActionResult> SendMatch(int receiverPetId, int? senderPetId)
        {
            EnsurePetMatchesTable();

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (currentUserId == null)
                return RedirectToAction("Login", "Account");

            var senderPet = _context.Pets
                .FirstOrDefault(x =>
                    x.UserId == currentUserId &&
                    (!senderPetId.HasValue || x.Id == senderPetId.Value));

            if (senderPet == null)
                return RedirectToAction("MyPets", "Pet");

            var receiverPet = _context.Pets
                .FirstOrDefault(x => x.Id == receiverPetId);

            if (receiverPet == null)
                return NotFound();

            if (receiverPet.UserId == currentUserId)
                return RedirectToAction("MyPets", "Pet");

            if (receiverPet.Species != senderPet.Species)
                return RedirectToAction("Index", new { communityPetId = receiverPetId });

            bool pairUnavailable = _context.PetMatches.Any(x =>
                (x.Status == "Pending" || x.Status == "Accepted") &&
                (x.SenderPetId == senderPet.Id ||
                 x.ReceiverPetId == senderPet.Id ||
                 x.SenderPetId == receiverPet.Id ||
                 x.ReceiverPetId == receiverPet.Id));

            if (pairUnavailable)
                return RedirectToAction("Index", new { communityPetId = receiverPetId });

            bool existed = _context.PetMatches.Any(x =>
                (x.SenderPetId == senderPet.Id && x.ReceiverPetId == receiverPetId) ||
                (x.SenderPetId == receiverPetId && x.ReceiverPetId == senderPet.Id));

            if (!existed)
            {
                var match = new PetMatch
                {
                    SenderPetId = senderPet.Id,
                    ReceiverPetId = receiverPetId,
                    Status = "Accepted",
                    CreatedAt = DateTime.Now
                };

                _context.PetMatches.Add(match);

                var notification = new Notification
                {
                    UserId = receiverPet.UserId,
                    Title = "Kết nối thú cưng mới",
                    Content = $"{senderPet.Name} đã ghép đôi với {receiverPet.Name}",
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

            return RedirectToAction("Matches");
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Cancel(int id)
        {
            EnsurePetMatchesTable();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == null)
                return RedirectToAction("Login", "Account");

            var match = _context.PetMatches
                .Include(x => x.SenderPet)
                .Include(x => x.ReceiverPet)
                .FirstOrDefault(x =>
                    x.Id == id &&
                    (x.SenderPet.UserId == currentUserId ||
                     x.ReceiverPet.UserId == currentUserId));

            if (match == null)
                return NotFound();

            _context.PetMatches.Remove(match);
            await _context.SaveChangesAsync();

            return RedirectToAction(match.Status == "Pending" ? "Requests" : "Matches");
        }

        public IActionResult Requests()
        {
            EnsurePetMatchesTable();

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var myPetIds = _context.Pets
                .Where(x => x.UserId == currentUserId)
                .Select(x => x.Id)
                .ToList();

            if (!myPetIds.Any())
                return RedirectToAction("Index");

            var requests = _context.PetMatches
                .Include(x => x.SenderPet)
                .Include(x => x.ReceiverPet)
                .Where(x =>
                    myPetIds.Contains(x.ReceiverPetId) &&
                    x.Status == "Pending")
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            return View(requests);
        }

        public async Task<IActionResult> Accept(int id)
        {
            EnsurePetMatchesTable();

            var match = _context.PetMatches
                .Include(x => x.SenderPet)
                .Include(x => x.ReceiverPet)
                .FirstOrDefault(x => x.Id == id);

            if (match == null)
                return NotFound();

            match.Status = "Accepted";
            match.CreatedAt = DateTime.Now;

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
            EnsurePetMatchesTable();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var myPetIds = _context.Pets
                .Where(x => x.UserId == currentUserId)
                .Select(x => x.Id)
                .ToList();

            if (!myPetIds.Any())
                return RedirectToAction("MyPets", "Pet");

            ViewBag.CurrentUserId = currentUserId;

            var matches = _context.PetMatches
                .Include(x => x.SenderPet)
                .Include(x => x.ReceiverPet)
                .Where(x =>
                    (myPetIds.Contains(x.SenderPetId) ||
                     myPetIds.Contains(x.ReceiverPetId))
                    &&
                    x.Status == "Accepted")
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            return View(matches);
        }

        private void EnsurePetMatchesTable()
        {
            _context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[dbo].[PetMatches]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PetMatches]
    (
        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_PetMatches] PRIMARY KEY,
        [SenderPetId] int NOT NULL,
        [ReceiverPetId] int NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [FK_PetMatches_Pets_SenderPetId]
            FOREIGN KEY ([SenderPetId]) REFERENCES [dbo].[Pets]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PetMatches_Pets_ReceiverPetId]
            FOREIGN KEY ([ReceiverPetId]) REFERENCES [dbo].[Pets]([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_PetMatches_SenderPetId]
        ON [dbo].[PetMatches]([SenderPetId]);

    CREATE INDEX [IX_PetMatches_ReceiverPetId]
        ON [dbo].[PetMatches]([ReceiverPetId]);
END;

IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM [dbo].[__EFMigrationsHistory]
        WHERE [MigrationId] = N'20260617120000_AddPetMatches'
   )
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260617120000_AddPetMatches', N'8.0.0');
END;
");
        }
    }
}
