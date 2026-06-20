using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.Models;
using PetSocial.ViewModels;

namespace PetSocial.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PetController : Controller
    {
        private const int PageSize = 20;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public PetController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? searchString,
            string? species = "all",
            string? completeness = "all",
            string? sort = "newest",
            int page = 1)
        {
            species = string.IsNullOrWhiteSpace(species) ? "all" : species.Trim();
            completeness = string.IsNullOrWhiteSpace(completeness) ? "all" : completeness.Trim().ToLowerInvariant();
            sort = sort == "oldest" || sort == "name" || sort == "score" ? sort : "newest";

            var query = _context.Pets
                .AsNoTracking()
                .Include(p => p.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();
                var lowerSearch = searchString.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(lowerSearch) ||
                    p.Species.ToLower().Contains(lowerSearch) ||
                    (p.Breed != null && p.Breed.ToLower().Contains(lowerSearch)) ||
                    (p.Location != null && p.Location.ToLower().Contains(lowerSearch)) ||
                    p.User.FullName.ToLower().Contains(lowerSearch) ||
                    (p.User.Email != null && p.User.Email.ToLower().Contains(lowerSearch)));
            }

            if (species != "all")
            {
                query = query.Where(p => p.Species == species);
            }

            query = completeness switch
            {
                "complete" => query.Where(p =>
                    p.AvatarUrl != null && p.AvatarUrl != "" &&
                    p.Breed != null && p.Breed != "" &&
                    p.Gender != null && p.Gender != "" &&
                    p.Personality != null && p.Personality != "" &&
                    p.Location != null && p.Location != "" &&
                    p.Description != null && p.Description != ""),
                "needs-review" => query.Where(p =>
                    p.AvatarUrl == null || p.AvatarUrl == "" ||
                    p.Breed == null || p.Breed == "" ||
                    p.Gender == null || p.Gender == "" ||
                    p.Personality == null || p.Personality == "" ||
                    p.Location == null || p.Location == "" ||
                    p.Description == null || p.Description == ""),
                _ => query
            };

            query = sort switch
            {
                "oldest" => query.OrderBy(p => p.Id),
                "name" => query.OrderBy(p => p.Name),
                "score" => query.OrderBy(p => p.AvatarUrl == null || p.AvatarUrl == "")
                    .ThenBy(p => p.Description == null || p.Description == "")
                    .ThenBy(p => p.Personality == null || p.Personality == "")
                    .ThenBy(p => p.Name),
                _ => query.OrderByDescending(p => p.Id)
            };

            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var pets = await query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var model = pets.Select(ToManageVM).ToList();

            ViewBag.SearchString = searchString;
            ViewBag.Species = species;
            ViewBag.Completeness = completeness;
            ViewBag.Sort = sort;
            ViewBag.Page = page;
            ViewBag.PageSize = PageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalPets = await _context.Pets.CountAsync();
            ViewBag.TotalOwners = await _context.Pets.Select(p => p.UserId).Distinct().CountAsync();
            ViewBag.SpeciesCount = await _context.Pets
                .Where(p => p.Species != "")
                .Select(p => p.Species)
                .Distinct()
                .CountAsync();
            ViewBag.MissingPhoto = await _context.Pets.CountAsync(p => p.AvatarUrl == null || p.AvatarUrl == "");
            ViewBag.CompleteProfiles = await _context.Pets.CountAsync(p =>
                p.AvatarUrl != null && p.AvatarUrl != "" &&
                p.Breed != null && p.Breed != "" &&
                p.Gender != null && p.Gender != "" &&
                p.Personality != null && p.Personality != "" &&
                p.Location != null && p.Location != "" &&
                p.Description != null && p.Description != "");
            ViewBag.SpeciesOptions = await _context.Pets
                .Where(p => p.Species != "")
                .Select(p => p.Species)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            int id,
            string? searchString,
            string? species,
            string? completeness,
            string? sort,
            int page = 1)
        {
            var pet = await _context.Pets.FindAsync(id);
            var routeValues = new { searchString, species, completeness, sort, page };

            if (pet == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ thú cưng cần xóa.";
                return RedirectToAction(nameof(Index), routeValues);
            }

            try
            {
                DeleteLocalImage(pet.AvatarUrl);
                _context.Pets.Remove(pet);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa hồ sơ thú cưng {pet.Name}.";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] = "Không thể xóa hồ sơ này vì đang có dữ liệu liên quan. Hãy kiểm tra lịch sử ghép đôi hoặc nội dung liên kết trước.";
            }

            return RedirectToAction(nameof(Index), routeValues);
        }

        private static PetManageVM ToManageVM(PetModule pet)
        {
            var score = CalculateProfileScore(pet);

            return new PetManageVM
            {
                Id = pet.Id,
                Name = pet.Name,
                Species = string.IsNullOrWhiteSpace(pet.Species) ? "Chưa cập nhật" : pet.Species,
                Breed = string.IsNullOrWhiteSpace(pet.Breed) ? "Chưa cập nhật" : pet.Breed,
                Age = pet.Age,
                Gender = string.IsNullOrWhiteSpace(pet.Gender) ? "Chưa cập nhật" : pet.Gender,
                Location = string.IsNullOrWhiteSpace(pet.Location) ? "Chưa cập nhật" : pet.Location,
                Weight = pet.Weight,
                Personality = string.IsNullOrWhiteSpace(pet.Personality) ? "Chưa cập nhật" : pet.Personality,
                Description = pet.Description ?? string.Empty,
                AvatarUrl = pet.AvatarUrl ?? string.Empty,
                OwnerId = pet.UserId,
                OwnerName = string.IsNullOrWhiteSpace(pet.User?.FullName) ? "Chưa cập nhật" : pet.User.FullName,
                OwnerEmail = pet.User?.Email ?? string.Empty,
                ProfileScore = score,
                HasPhoto = !string.IsNullOrWhiteSpace(pet.AvatarUrl),
                IsComplete = score >= 80
            };
        }

        private static int CalculateProfileScore(PetModule pet)
        {
            var completedFields = 0;
            const int totalFields = 9;

            if (!string.IsNullOrWhiteSpace(pet.AvatarUrl)) completedFields++;
            if (!string.IsNullOrWhiteSpace(pet.Species)) completedFields++;
            if (!string.IsNullOrWhiteSpace(pet.Breed)) completedFields++;
            if (!string.IsNullOrWhiteSpace(pet.Gender)) completedFields++;
            if (!string.IsNullOrWhiteSpace(pet.FurColor)) completedFields++;
            if (pet.Weight.HasValue) completedFields++;
            if (!string.IsNullOrWhiteSpace(pet.Personality)) completedFields++;
            if (!string.IsNullOrWhiteSpace(pet.Location)) completedFields++;
            if (!string.IsNullOrWhiteSpace(pet.Description)) completedFields++;

            return (int)Math.Round(completedFields * 100m / totalFields);
        }

        private void DeleteLocalImage(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/images/Pet/", StringComparison.OrdinalIgnoreCase))
                return;

            var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
    }
}
