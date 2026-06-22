namespace PetSocial.ViewModels
{
    public class UserManageVM
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime JoinDate { get; set; }
        public string AvatarUrl { get; set; } = string.Empty;

        // Thống kê đóng góp
        public int PetCount { get; set; }
        public int PostCount { get; set; }
        public int ViolationCount { get; set; }

        // Trạng thái và Phân quyền
        public bool IsActive { get; set; }
        public bool IsAdmin { get; set; }
    }
}
