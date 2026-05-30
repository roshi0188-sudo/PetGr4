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
        public int Age { get; set; }
        public string? AvatarUrl { get; set; }

        // Foreign Key
        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public AppUser User { get; set; } = null!;
    }
}