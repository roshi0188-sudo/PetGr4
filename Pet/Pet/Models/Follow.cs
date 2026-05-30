using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetSocial.Models
{
    public class Follow
    {
        [Key]
        public int Id { get; set; }

        // Người nhấn nút theo dõi
        public string FollowerId { get; set; } = string.Empty;
        [ForeignKey("FollowerId")]
        public AppUser Follower { get; set; } = null!;

        // Người được theo dõi
        public string FollowingId { get; set; } = string.Empty;
        [ForeignKey("FollowingId")]
        public AppUser Following { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}