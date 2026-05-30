using System.ComponentModel.DataAnnotations;

namespace PetSocial.ViewModels
{
    public class UserProfileVM
    {
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        public string? CurrentAvatarUrl { get; set; }

        [Display(Name = "Ảnh đại diện mới")]
        public IFormFile? AvatarFile { get; set; }
    }
}