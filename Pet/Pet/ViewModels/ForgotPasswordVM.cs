using System.ComponentModel.DataAnnotations;

namespace PetSocial.ViewModels
{
    // 1. Model dùng cho trang nhập Email gửi yêu cầu
    public class ForgotPasswordVM
    {
        [Required(ErrorMessage = "Vui lòng nhập Email của bạn.")]
        [EmailAddress(ErrorMessage = "Định dạng Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;
    }

    // 2. Model dùng cho trang thiết lập mật khẩu mới sau khi xác thực thành công
    public class ResetPasswordVM
    {
        public string Token { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận lại mật khẩu.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}