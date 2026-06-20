using PetSocial.Models;

namespace PetSocial.ViewModels
{
    public class AdminPostManagementVM
    {
        public int TotalPosts { get; set; }
        public int TotalReports { get; set; }
        public int PendingReports { get; set; }
        public int ReportedPosts { get; set; }
        public string? Search { get; set; }
        public List<Post> Posts { get; set; } = new();
        public List<string> ChartLabels { get; set; } = new();
        public List<int> ChartValues { get; set; } = new();
    }
}
