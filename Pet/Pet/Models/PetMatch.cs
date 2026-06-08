using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetSocial.Models
{
    public class PetMatch
    {
        [Key]
        public int Id { get; set; }

        public int SenderPetId { get; set; }

        public int ReceiverPetId { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(SenderPetId))]
        public PetModule SenderPet { get; set; } = null!;

        [ForeignKey(nameof(ReceiverPetId))]
        public PetModule ReceiverPet { get; set; } = null!;
    }
}
