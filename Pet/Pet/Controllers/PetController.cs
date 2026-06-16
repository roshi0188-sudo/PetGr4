using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetSocial.Data;
using PetSocial.Models;
using System.Globalization;

namespace PetSocial.Controllers
{
    public class PetController : Controller
    {
        private const int PageSize = 12;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public PetController(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        [Authorize]
        public async Task<IActionResult> Index(
            string? searchString,
            string? ageRange,
            string? gender,
            string? personality,
            string? breed,
            int page = 1)
        {
            // 1. Khởi tạo câu truy vấn gốc
            var query = _context.Pets.AsNoTracking().Include(p => p.User).AsQueryable();

            if (!User.IsInRole("Admin"))
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId)) return Challenge();

                query = query.Where(p => p.UserId == userId);
            }
            // ========================================================

            // 2. Các bộ lọc tìm kiếm (Giữ nguyên logic cũ của nhóm)
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(p =>
                    p.Name.Contains(searchString) ||
                    p.Species.Contains(searchString) ||
                    (p.Breed != null && p.Breed.Contains(searchString)));
            }

            query = ageRange switch
            {
                "under1" => query.Where(p => p.Age < 1),
                "1to3" => query.Where(p => p.Age >= 1 && p.Age <= 3),
                "over3" => query.Where(p => p.Age > 3),
                _ => query
            };

            if (!string.IsNullOrWhiteSpace(gender))
                query = query.Where(p => p.Gender == gender);

            if (!string.IsNullOrWhiteSpace(personality))
                query = query.Where(p => p.Personality != null && p.Personality.Contains(personality));

            if (!string.IsNullOrWhiteSpace(breed))
                query = query.Where(p => p.Breed != null && p.Breed.Contains(breed));

            // Phân trang
            var totalPets = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalPets / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var pets = await query
                .OrderByDescending(p => p.Id)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Truyền dữ liệu bổ trợ sang View để xử lý ẩn hiện nút bấm
            ViewBag.SearchString = searchString;
            ViewBag.AgeRange = ageRange;
            ViewBag.Gender = gender;
            ViewBag.Personality = personality;
            ViewBag.Breed = breed;
            ViewBag.TotalPetCount = totalPets;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            // Gửi thêm ID người dùng hiện tại và trạng thái Admin để View check quyền
            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            ViewBag.IsAdmin = User.IsInRole("Admin");

            return View(pets);
        }

        public async Task<IActionResult> Details(int id)
        {
            var pet = await _context.Pets.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
            if (pet == null) return NotFound();

            return View(pet);
        }

        [Authorize]
        public IActionResult Create() => View(new PetModule());

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PetModule pet, IFormFile? imageFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            pet.UserId = user.Id;
            pet.Weight = ParseNullableDecimal(Request.Form[nameof(PetModule.Weight)]);
            ModelState.Remove(nameof(PetModule.User));
            ModelState.Remove(nameof(PetModule.UserId));
            ModelState.Remove(nameof(PetModule.Weight));

            if (!ModelState.IsValid) return View(pet);

            pet.AvatarUrl = await SavePetImageAsync(imageFile);
            _context.Pets.Add(pet);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = pet.Id });
        }

        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var pet = await _context.Pets.FindAsync(id);
            if (pet == null) return NotFound();
            if (!await CanManagePetAsync(pet)) return Forbid();

            return View(pet);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PetModule formPet, IFormFile? imageFile)
        {
            if (id != formPet.Id) return BadRequest();

            var pet = await _context.Pets.FindAsync(id);
            if (pet == null) return NotFound();
            if (!await CanManagePetAsync(pet)) return Forbid();

            ModelState.Remove(nameof(PetModule.User));
            ModelState.Remove(nameof(PetModule.UserId));
            formPet.Weight = ParseNullableDecimal(Request.Form[nameof(PetModule.Weight)]);
            ModelState.Remove(nameof(PetModule.Weight));

            if (!ModelState.IsValid)
            {
                formPet.UserId = pet.UserId;
                formPet.AvatarUrl = pet.AvatarUrl;
                return View(formPet);
            }

            pet.Name = formPet.Name;
            pet.Species = formPet.Species;
            pet.Breed = formPet.Breed;
            pet.Age = formPet.Age;
            pet.Gender = formPet.Gender;
            pet.FurColor = formPet.FurColor;
            pet.Weight = formPet.Weight;
            pet.Personality = formPet.Personality;
            pet.Hobbies = formPet.Hobbies;
            pet.Location = formPet.Location;
            pet.Description = formPet.Description;

            var newImageUrl = await SavePetImageAsync(imageFile);
            if (!string.IsNullOrWhiteSpace(newImageUrl))
            {
                DeleteLocalImage(pet.AvatarUrl);
                pet.AvatarUrl = newImageUrl;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = pet.Id });
        }

        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var pet = await _context.Pets.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
            if (pet == null) return NotFound();
            if (!await CanManagePetAsync(pet)) return Forbid();

            return View(pet);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var pet = await _context.Pets.FindAsync(id);
            if (pet == null) return NotFound();
            if (!await CanManagePetAsync(pet)) return Forbid();

            DeleteLocalImage(pet.AvatarUrl);
            _context.Pets.Remove(pet);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> CanManagePetAsync(PetModule pet)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;

            return pet.UserId == user.Id || User.IsInRole("Admin");
        }

        private async Task<string?> SavePetImageAsync(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) return null;
            if (string.IsNullOrWhiteSpace(imageFile.ContentType) || !imageFile.ContentType.StartsWith("image/"))
                return null;

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "Pet");
            Directory.CreateDirectory(uploadsFolder);

            var extension = Path.GetExtension(imageFile.FileName);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await imageFile.CopyToAsync(stream);

            return $"/images/Pet/{fileName}";
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

        private static decimal? ParseNullableDecimal(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var normalizedValue = value.Trim().Replace(',', '.');
            return decimal.TryParse(normalizedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
                ? result
                : null;
        }
    }
}