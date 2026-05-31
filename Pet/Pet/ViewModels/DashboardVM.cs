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