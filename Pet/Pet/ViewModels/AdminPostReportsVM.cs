using PetSocial.Models;

namespace PetSocial.ViewModels
{
    public class AdminPostReportsVM
    {
        public string Status { get; set; } = "Pending";
        public int PendingCount { get; set; }
        public int ResolvedCount { get; set; }
        public int DismissedCount { get; set; }
        public List<PostReport> Reports { get; set; } = new();
    }
}
