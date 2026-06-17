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
                .Where(x => x.UserId == currentUserId);

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
                    .OrderByDescending(x => x.Id)
                    .ToList();
            }

            ViewBag.MyPets = myPets;
            ViewBag.SelectedMyPet = selectedMyPet;
            ViewBag.SelectedCommunityPet = communityPet;
            ViewBag.ShowSuggestions = showSuggestions;
            ViewBag.TotalSuggestions = pets.Count;

            // Đếm lời mời đang chờ
            if (!string.IsNullOrEmpty(currentUserId))
            {
                var myPetIds = _context.Pets
                    .Where(x => x.UserId == currentUserId)
                    .Select(x => x.Id)
                    .ToList();

                ViewBag.PendingCount = _context.PetMatches
                    .Count(x =>
                        myPetIds.Contains(x.ReceiverPetId)
                        && x.Status == "Pending");
            }
            else
            {
                ViewBag.PendingCount = 0;
            }

            // Trạng thái giữa 2 pet đã chọn
            ViewBag.MatchStatus = null;
            ViewBag.CanSendRequest = true;

            if (selectedMyPet != null && communityPet != null)
            {
                var existingMatch = _context.PetMatches
                    .FirstOrDefault(x =>
                        (x.SenderPetId == selectedMyPet.Id &&
                         x.ReceiverPetId == communityPet.Id)
                        ||
                        (x.SenderPetId == communityPet.Id &&
                         x.ReceiverPetId == selectedMyPet.Id));

                if (existingMatch != null)
                {
                    ViewBag.CanSendRequest = false;

                    if (existingMatch.Status == "Pending")
                    {
                        ViewBag.MatchStatus =
                            "Đã tồn tại lời mời kết nối đang chờ duyệt.";
                    }
                    else if (existingMatch.Status == "Accepted")
                    {
                        ViewBag.MatchStatus =
                            "Hai thú cưng này đã kết nối thành công.";
                    }
                }
                else if (!SameSpecies(selectedMyPet.Species, communityPet.Species))
                {
                    ViewBag.CanSendRequest = false;
                    ViewBag.MatchStatus =
                        "Hai thú cưng đang khác loài nên chưa thể gửi lời mời kết nối.";
                }
            }

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
        [ValidateAntiForgeryToken]
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

            if (!SameSpecies(receiverPet.Species, senderPet.Species))
            {
                TempData["Error"] =
                    "Hai thú cưng đang khác loài nên chưa thể gửi lời mời kết nối.";

                return RedirectToAction(nameof(Index), new { communityPetId = receiverPetId, myPetId = senderPet.Id });
            }

            // Kiểm tra tồn tại cả hai chiều cho trạng thái Pending
            bool existed = _context.PetMatches.Any(x =>
                ((x.SenderPetId == senderPet.Id && x.ReceiverPetId == receiverPetId) ||
                 (x.SenderPetId == receiverPetId && x.ReceiverPetId == senderPet.Id))
                && (x.Status == "Pending" || x.Status == "Accepted"));

            if (!existed)
            {
                var match = new PetMatch
                {
                    SenderPetId = senderPet.Id,
                    ReceiverPetId = receiverPetId,
                    Status = "Pending",
                    CreatedAt = DateTime.Now
                };

                _context.PetMatches.Add(match);

                var notification = new Notification
                {
                    UserId = receiverPet.UserId,
                    Title = "Lời mời kết nối",
                    Content = $"{senderPet.Name} muốn kết nối với {receiverPet.Name}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };

                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();

                await _hubContext.Clients.User(receiverPet.UserId)
                    .SendAsync(
                        "ReceiveNotification",
                        notification.Title,
                        notification.Content,
                        notification.CreatedAt.ToString("HH:mm"));

                TempData["Success"] =
                    "Đã gửi lời mời kết nối.";
            }
            else
            {
                TempData["Error"] =
                    "Lời mời đã tồn tại.";
            }

            // Redirect về Index và giữ chọn pet để UI hiển thị đúng
            return RedirectToAction(nameof(Index), new { communityPetId = receiverPetId, myPetId = senderPet.Id });
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

        [Authorize]
        public async Task<IActionResult> Accept(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var match = _context.PetMatches
                .Include(x => x.SenderPet)
                .Include(x => x.ReceiverPet)
                .FirstOrDefault(x => x.Id == id);

            if (match == null)
                return NotFound();

            if (match.ReceiverPet.UserId != currentUserId)
                return Forbid();

            if (match.Status != "Pending")
                return RedirectToAction(nameof(Requests));

            match.Status = "Accepted";

            var notification = new Notification
            {
                UserId = match.SenderPet.UserId,
                Title = "Ghép đôi thành công",
                Content =
                    $"{match.ReceiverPet.Name} đã chấp nhận lời mời kết nối",
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            await _hubContext.Clients.User(match.SenderPet.UserId)
                .SendAsync(
                    "ReceiveNotification",
                    notification.Title,
                    notification.Content,
                    DateTime.Now.ToString("HH:mm"));

            TempData["Success"] =
                "Đã chấp nhận lời mời kết nối.";

            return RedirectToAction(nameof(Requests));
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

        private static bool SameSpecies(string? left, string? right)
        {
            return string.Equals(
                left?.Trim(),
                right?.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
