using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetSocial.Models
{
    public class PetModule
    {
        [Key]
        public int Id { get; set; }
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty; // Chó, Mèo, etc.
        public string? Breed { get; set; }
        public int Age { get; set; }
        public string? Gender { get; set; }
        public string? FurColor { get; set; }
        public decimal? Weight { get; set; }
        public string? Personality { get; set; }
        public string? Hobbies { get; set; }
        public string? Location { get; set; }
        public string? Description { get; set; }
        public string? AvatarUrl { get; set; }

        // Foreign Key
        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public AppUser User { get; set; } = null!;
    }
}