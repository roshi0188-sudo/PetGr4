using Microsoft.AspNetCore.Identity;

namespace PetSocial.Models
{
    public class AppUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Relationships
        public ICollection<PetModule> Pets { get; set; } = new List<PetModule>();
        public ICollection<Post> Posts { get; set; } = new List<Post>();
        public ICollection<Follow> Followers { get; set; } = new List<Follow>();
        public ICollection<Follow> Following { get; set; } = new List<Follow>();
    }
}