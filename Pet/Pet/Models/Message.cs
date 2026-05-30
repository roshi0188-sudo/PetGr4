using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetSocial.Models
{
    public class Message
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tin nhắn không được để trống")]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        // Người gửi
        public string SenderId { get; set; } = string.Empty;
        [ForeignKey("SenderId")]
        public AppUser Sender { get; set; } = null!;

        // Người nhận
        public string ReceiverId { get; set; } = string.Empty;
        [ForeignKey("ReceiverId")]
        public AppUser Receiver { get; set; } = null!;
    }
}