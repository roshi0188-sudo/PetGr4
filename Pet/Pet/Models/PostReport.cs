using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetSocial.Models
{
    public class PostReport
    {
        [Key]
        public int Id { get; set; }

        public int PostId { get; set; }
        [ForeignKey(nameof(PostId))]
        public Post Post { get; set; } = null!;

        public string ReporterId { get; set; } = string.Empty;
        [ForeignKey(nameof(ReporterId))]
        public AppUser Reporter { get; set; } = null!;

        [MaxLength(300)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(30)]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ReviewedAt { get; set; }

        public string? ReviewedByAdminId { get; set; }

        [MaxLength(500)]
        public string? AdminNote { get; set; }
    }
}
