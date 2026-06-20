namespace PetSocial.ViewModels
{
    public class DashboardVM
    {
        public int TotalUsers { get; set; }
        public int TotalPets { get; set; }
        public int TotalPosts { get; set; }
        public int TotalComments { get; set; }
        public int TotalLikes { get; set; }
        public int TotalMessages { get; set; }
        public List<UserDashboardVM> Users { get; set; } = new List<UserDashboardVM>();
        // Weekly / Monthly series for charts
        public List<string> WeeklyLabels { get; set; } = new List<string>();
        public List<int> WeeklyNewUsers { get; set; } = new List<int>();
        public List<int> WeeklyNewPosts { get; set; } = new List<int>();

        public List<string> MonthlyLabels { get; set; } = new List<string>();
        public List<int> MonthlyNewUsers { get; set; } = new List<int>();
        public List<int> MonthlyNewPosts { get; set; } = new List<int>();
    }

    public class UserDashboardVM
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public DateTime JoinDate { get; set; }
        public int PetCount { get; set; }
        public int PostCount { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
    }
}
