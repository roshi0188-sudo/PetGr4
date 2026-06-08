using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PetSocial.Models;

namespace PetSocial.Data
{
    public class ApplicationDbContext : IdentityDbContext<AppUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<PetModule> Pets { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<Follow> Follows { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<PetMatch> PetMatches { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 1. Cấu hình bảng Identity mặc định sang tên tùy chọn gọn hơn
            builder.Entity<AppUser>().ToTable("Users");
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>().ToTable("Roles");

            builder.Entity<PetModule>()
                .Property(p => p.Weight)
                .HasPrecision(5, 2);

            // 2. Mối quan hệ bảng Comment
            builder.Entity<Comment>()
                .HasOne(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa Post sẽ tự động xóa luôn các Comment của bài đó

            builder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Không cho phép xóa User nếu họ có bình luận tồn tại (Tránh lỗi Cascade Cycle)

            // 3. Mối quan hệ bảng Like
            builder.Entity<Like>()
                .HasOne(l => l.Post)
                .WithMany(p => p.Likes)
                .HasForeignKey(l => l.PostId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa Post tự động xóa Like

            builder.Entity<Like>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Đảm bảo một User chỉ được Like một Post tối đa 1 lần (Unique Constraint)
            builder.Entity<Like>()
                .HasIndex(l => new { l.UserId, l.PostId })
                .IsUnique();

            // 4. Mối quan hệ bảng Follow (Tự tham chiếu)
            builder.Entity<Follow>()
                .HasOne(f => f.Follower)
                .WithMany(u => u.Following)
                .HasForeignKey(f => f.FollowerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Follow>()
                .HasOne(f => f.Following)
                .WithMany(u => u.Followers)
                .HasForeignKey(f => f.FollowingId)
                .OnDelete(DeleteBehavior.Restrict);

            // 5. Mối quan hệ bảng Message (Tin nhắn Chat giữa 2 User)
            builder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PetMatch>()
                .HasOne(pm => pm.SenderPet)
                .WithMany()
                .HasForeignKey(pm => pm.SenderPetId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PetMatch>()
                .HasOne(pm => pm.ReceiverPet)
                .WithMany()
                .HasForeignKey(pm => pm.ReceiverPetId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
