using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetSocial.Migrations
{
    /// <inheritdoc />
    public partial class Themdata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. KHAI BÁO GUID ĐỊNH DANH
            var roleAdminId = "role-admin";
            var roleUserId = "role-user";

            var uAdmin = "id-admin";
            var uLam = "id-lam";
            var uNgoc = "id-ngoc";
            var uAnh = "id-anh";
            var uNhu = "id-nhu";

            var defaultPassword = BCrypt.Net.BCrypt.HashPassword("123456");
            var now = DateTime.Now;

            // 2. CHÈN DỮ LIỆU CÁC NHÓM QUYỀN (Roles)
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" },
                values: new object[,]
                {
                    { roleAdminId, "Admin", "ADMIN", Guid.NewGuid().ToString() },
                    { roleUserId, "User", "USER", Guid.NewGuid().ToString() }
                });

            // 3. CHÈN TÀI KHOẢN NGƯỜI DÙNG
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
                    { uAdmin, "Quản trị viên", "admin@petsocial.com", "ADMIN@PETSOCIAL.COM", "admin@petsocial.com", "ADMIN@PETSOCIAL.COM", true, defaultPassword, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "0900000000", true, false, false, 0, new DateTime(2026, 1, 5) },
                    { uLam, "Hồng Lam", "lam@petsocial.com", "LAM@PETSOCIAL.COM", "lam@petsocial.com", "LAM@PETSOCIAL.COM", true, defaultPassword, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "0901111111", true, false, false, 0, new DateTime(2026, 1, 12) },
                    { uNgoc, "Kim Ngọc", "ngoc@petsocial.com", "NGOC@PETSOCIAL.COM", "ngoc@petsocial.com", "NGOC@PETSOCIAL.COM", true, defaultPassword, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "0902222222", true, false, false, 0, new DateTime(2026, 2, 18) },
                    { uAnh, "Chúc Anh", "anh@petsocial.com", "ANH@PETSOCIAL.COM", "anh@petsocial.com", "ANH@PETSOCIAL.COM", true, defaultPassword, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "0903333333", true, false, false, 0, new DateTime(2026, 2, 25) },
                    { uNhu, "Ngọc Như", "nhu@petsocial.com", "NHU@PETSOCIAL.COM", "nhu@petsocial.com", "NHU@PETSOCIAL.COM", true, defaultPassword, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "0904444444", true, false, false, 0, new DateTime(2026, 3, 10) }
                });

            // 4. PHÂN QUYỀN CHO USER (Bảng AspNetUserRoles)
            migrationBuilder.InsertData(
                table: "AspNetUserRoles",
                columns: new[] { "UserId", "RoleId" },
                values: new object[,]
                {
                    { uAdmin, roleAdminId },
                    { uLam, roleUserId },
                    { uNgoc, roleUserId },
                    { uAnh, roleUserId },
                    { uNhu, roleUserId }
                });

            // 5. CHÈN THÚ CƯNG MẪU 
            migrationBuilder.InsertData(
                table: "Pets",
                columns: new[]
                {
                    "Name", "Species", "Breed", "Age", "Gender", "FurColor", "Weight",
                    "Personality", "Hobbies", "Location", "Description", "AvatarUrl", "UserId"
                },
                values: new object[,]
                {
                    { "Milu", "Chó", "Corgi", 2, "Đực", "Vàng trắng", 9.80m, "Năng động", "Chạy công viên", "TP.HCM", "Bé rất ham ăn.", "/images/Pet/cho-corgi.jpg", uLam },
                    { "Ngáo", "Chó", "Husky", 3, "Đực", "Đen ngáo", 22.10m, "Ngáo ngơ", "Phá chuồng", "TP.HCM", "Biểu tượng hài hước.", "/images/Pet/husky.jpg", uLam },
                    { "Mimi", "Mèo", "Mèo Anh Lông Ngắn", 1, "Cái", "Xám trắng", 3.20m, "Dễ thương", "Ngủ nắng", "Hà Nội", "Mimi rất quấn người.", "/images/Pet/meo.jpg", uNgoc },
                    { "Mập", "Mèo", "Mèo Munchkin", 1, "Đực", "Tabby vằn", 2.90m, "Lười biếng", "Ăn và ngủ", "Vũng Tàu", "Chân siêu ngắn cực cute.", "/images/Pet/munchkin.jpg", uNgoc },
                    { "Lu Lu", "Chó", "Shiba Inu", 1, "Đực", "Vàng rơm", 10.50m, "Tinh nghịch", "Cười híp mắt", "Cần Thơ", "Thích ăn thịt gà.", "/images/Pet/shiba.jpg", uAnh },
                    { "Chít Chít", "Chuột", "Hamster Bear", 1, "Đực", "Trắng xám", 0.15m, "Nhút nhát", "Chạy bánh xe", "TP.HCM", "Bé nhỏ bằng lòng bàn tay.", "/images/Pet/hamster.jpg", uAnh },
                    { "Poodle Trắng", "Chó", "Poodle", 2, "Cái", "Trắng", 4.50m, "Thân thiện", "Chơi bóng", "Đà Nẵng", "Ngoan ngoãn, sạch sẽ.", "/images/Pet/poodle.jpg", uNhu },
                    { "Bông Bông", "Mèo", "Mèo Ba Tư", 2, "Cái", "Trắng muốt", 4.10m, "Chảnh chọe", "Nằm điều hòa", "Hải Phòng", "Lông siêu dày.", "/images/Pet/persian.jpg", uNhu }
                });

            // 6. CHÈN BÀI VIẾT 
            migrationBuilder.InsertData(
                table: "Posts",
                columns: new[] { "Content", "ImageUrl", "CreatedAt", "IsRemovedByAi", "UserId" },
                values: new object[,]
                {
                    // Lịch sử các tháng trước
                    { "Hôm nay dẫn bé Milu đi dạo mát mẻ quá.", null, new DateTime(2026, 1, 20), false, uLam },
                    { "Mimi nhà mình mới đổi loại hạt ăn liền mới.", null, new DateTime(2026, 2, 22), false, uNgoc },
                    { "Bé Poodle mới cắt tỉa lông nhìn như cục bông gòn.", null, new DateTime(2026, 3, 15), false, uNhu },
                    { "Có ai có kinh nghiệm trị ve rận cho chó Husky không?", null, new DateTime(2026, 4, 12), false, uLam },
                    { "Mới sắm quả chuồng mới cho Shiba, nhìn thích mê.", null, new DateTime(2026, 5, 19), false, uAnh },
                    
                    // Thống kê chi tiết rải đều 7 ngày gần đây
                    { "Cảnh báo dịch sốt ve mùa này nhé các sen ơi!", null, now.AddDays(-6), false, uAdmin },
                    { "Bé Hamster nhà mình biết làm trò lộn nhào rồi nè.", null, now.AddDays(-5), false, uAnh },
                    { "Mèo chân ngắn ăn nhiều quá béo phì rồi phải làm sao?", null, now.AddDays(-4), false, uNgoc },
                    { "Khoe ảnh boss yêu ngủ ngửa bụng siêu hài hước.", null, now.AddDays(-3), false, uNhu },
                    { "Có ai rảnh tối nay lập hội offline khu vực công viên không?", null, now.AddDays(-2), false, uLam },
                    { "Chia sẻ công thức làm pate gan tại nhà siêu ngon.", null, now.AddDays(-1), false, uAnh }
                });

            // 7. CHÈN TIN NHẮN 
            migrationBuilder.InsertData(
                table: "Messages",
                columns: new[] { "Content", "CreatedAt", "IsRead", "SenderId", "ReceiverId" },
                values: new object[,]
                {
                    { "Chào Ngọc, Milu ăn hạt loại gì thế?", now.AddDays(-2), true, uNgoc, uLam },
                    { "Chào Ngọc, bé ăn loại Royal Canin nha.", now.AddDays(-2), true, uLam, uNgoc },
                    { "Chúc Anh ơi, tối nay đi dạo nha.", now.AddHours(-3), false, uLam, uAnh },
                    { "Okie, chốt.", now.AddHours(-2), true, uAnh, uLam },
                    { "Như ơi, tối thứ tư rảnh không cùng đi dạo?", now.AddHours(-1), false, uAdmin, uNhu }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
