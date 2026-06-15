using PetSocial.Models;

namespace PetSocial.ViewModels
{
    public class ProfileDetails
    {
        public AppUser User { get; set; } = null!;

        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }
        public int PostCount { get; set; }

        // Có phải là trang hồ sơ của chính mình không
        public bool IsOwnProfile { get; set; }

        // Người dùng hiện tại đã theo dõi người này hay chưa
        public bool IsFollowing { get; set; }

        public List<AppUser> Followers { get; set; } = new();
        public List<AppUser> FollowingList { get; set; } = new();

        public List<Post> Posts { get; set; } = new();
    }
}
