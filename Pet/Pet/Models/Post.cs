using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetSocial.Models
{
    public class Post
    {
        [Key]
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public AppUser User { get; set; } = null!;

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<Like> Likes { get; set; } = new List<Like>();
        public ICollection<PostReport> Reports { get; set; } = new List<PostReport>();
    }
}
