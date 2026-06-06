using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetSocial.Migrations
{
    /// <inheritdoc />
    public partial class ThemDL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Tạo GUID cố định để dễ dàng mapping khóa ngoại giữa các bảng
            var roleAdminId = "11111111-1111-1111-1111-111111111111";
            var roleUserId = "22222222-2222-2222-2222-222222222222";

            var adminId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            var lamId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
            var ngocId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
            var anhId = "dddddddd-dddd-dddd-dddd-dddddddddddd";
            var nhuId = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";

            // Băm mật khẩu chung "123456" cho tất cả tài khoản mẫu bằng BCrypt chuyên dụng của dự án
            var defaultPassword = BCrypt.Net.BCrypt.HashPassword("123456");
            var now = DateTime.Now;

            // 2. Insert Roles (Bảng Roles đã được đổi tên trong DbContext)
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" },
                values: new object[,]
                {
                    { roleAdminId, "Admin", "ADMIN", Guid.NewGuid().ToString() },
                    { roleUserId, "User", "USER", Guid.NewGuid().ToString() }
                });

            // 3. Insert Users (Bảng Users)
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[]
                {
                    "Id", "FullName", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
                    "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp",
                    "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount", "CreatedAt"
                },
                values: new object[,]
                {
                    { adminId, "Quản trị viên", "admin@petsocial.com", "ADMIN@PETSOCIAL.COM", "admin@petsocial.com", "ADMIN@PETSOCIAL.COM", true, defaultPassword, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "0900000000", false, false, false, 0, now },
                    { lamId, "Hồng Lam", "lam@petsocial.com", "LAM@PETSOCIAL.COM", "lam@petsocial.com", "LAM@PETSOCIAL.COM", true, defaultPassword, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "0901111111", false, false, false, 0, now },
                    { ngocId, "Kim Ngọc", "ngoc@petsocial.com", "NGOC@PETSOCIAL.COM", "ngoc@petsocial.com", "NGOC@PETSOCIAL.COM", true, defaultPassword, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "0902222222", false, false, false, 0, now },
                    { anhId, "Chúc Anh", "anh@petsocial.com", "ANH@PETSOCIAL.COM", "anh@petsocial.com", "ANH@PETSOCIAL.COM", true, defaultPassword, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "0903333333", false, false, false, 0, now },
                    { nhuId, "Ngọc Như", "nhu@petsocial.com", "NHU@PETSOCIAL.COM", "nhu@petsocial.com", "NHU@PETSOCIAL.COM", true, defaultPassword, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "0904444444", false, false, false, 0, now }
                });

            // 4. Cấp quyền (Bảng trung gian mặc định của Identity)
            migrationBuilder.InsertData(
                table: "AspNetUserRoles",
                columns: new[] { "UserId", "RoleId" },
                values: new object[,]
                {
                    { adminId, roleAdminId },
                    { lamId, roleUserId },
                    { ngocId, roleUserId },
                    { anhId, roleUserId },
                    { nhuId, roleUserId }
                });

            // 5. Insert Pets (Đã cập nhật theo model PetModule mới nhất của Ngọc)
            migrationBuilder.InsertData(
                table: "Pets",
                columns: new[]
                {
                    "Id", "Name", "Species", "Breed", "Age", "Gender",
                    "FurColor", "Weight", "Personality", "Hobbies",
                    "Location", "Description", "AvatarUrl", "UserId"
                },
                values: new object[,]
                {
                    {
                        1, "Milu", "Chó", "Corgi", 2, "Đực",
                        "Vàng Trắng", 8.5m, "Tinh nghịch, thân thiện", "Chạy bộ, bắt đĩa",
                        "Quận 1, TP.HCM", "Chú chó Corgi chân ngắn siêu quậy", "/images/Pet/cho-corgi.jpg", lamId
                    },
                    {
                        2, "Mimi", "Mèo", "Anh Lông Ngắn", 1, "Cái",
                        "Xám Xanh", 4.2m, "Chảnh, thích ngủ", "Ăn pate, cào móng",
                        "Bình Thạnh, TP.HCM", "Công chúa nhỏ thích làm nũng", "/images/Pet/meo.jpg", ngocId
                    }
                });

            // 6. Insert Posts (Dữ liệu test cho module của Anh)
            migrationBuilder.InsertData(
                table: "Posts",
                columns: new[] { "Id", "Content", "CreatedAt", "UserId" },
                values: new object[,]
                {
                    { 1, "Hôm nay dẫn Milu đi dạo công viên vui quá!", now, lamId },
                    { 2, "Mimi mới mua đồ chơi mới nè mọi người.", now.AddMinutes(30), ngocId },
                    { 3, "Chia sẻ kinh nghiệm chăm sóc chó cảnh mùa nóng.", now.AddHours(1), anhId }
                });

            // 7. Insert Comments
            migrationBuilder.InsertData(
                table: "Comments",
                columns: new[] { "Id", "Content", "CreatedAt", "PostId", "UserId" },
                values: new object[,]
                {
                    { 1, "Milu đáng yêu quá!", now.AddMinutes(10), 1, ngocId },
                    { 2, "Bài viết rất hữu ích, cảm ơn bạn.", now.AddHours(2), 3, nhuId }
                });

            // 8. Insert Likes
            migrationBuilder.InsertData(
                table: "Likes",
                columns: new[] { "Id", "CreatedAt", "PostId", "UserId" },
                values: new object[,]
                {
                    { 1, now.AddMinutes(5), 1, anhId },
                    { 2, now.AddMinutes(35), 2, lamId }
                });

            // 9. Insert Follows
            migrationBuilder.InsertData(
                table: "Follows",
                columns: new[] { "Id", "FollowerId", "FollowingId", "CreatedAt" },
                values: new object[,]
                {
                    { 1, lamId, ngocId, now },
                    { 2, ngocId, lamId, now },
                    { 3, nhuId, anhId, now }
                });

            // 10. Insert Messages (Dữ liệu test cho module Chat của Như)
            migrationBuilder.InsertData(
                table: "Messages",
                columns: new[] { "Id", "Content", "CreatedAt", "IsRead", "SenderId", "ReceiverId" },
                values: new object[,]
                {
                    { 1, "Chào Ngọc, dạo này Mimi khỏe không?", now, true, lamId, ngocId },
                    { 2, "Mimi khỏe nha, ăn ngoan lắm!", now.AddMinutes(2), false, ngocId, lamId }
                });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
