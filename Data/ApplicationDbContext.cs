using Microsoft.EntityFrameworkCore;
using Fruitables.Models;

namespace Fruitables.Data;

public class ApplicationDbContext : DbContext
{
    // Pre-generated BCrypt hash for "Admin@123" password
    // Generated using BCrypt.Net.BCrypt.HashPassword("Admin@123")
    private const string AdminPasswordHash = "$2a$11$lA/jMR6h6Qga83lrdc0xd.Fx1TLBOiefaI1vAvCcVTjhYFqTYisHO";

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewReport> ReviewReports => Set<ReviewReport>();
    public DbSet<ReviewHelpful> ReviewHelpfuls => Set<ReviewHelpful>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<SePayTransaction> SePayTransactions => Set<SePayTransaction>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<Testimonial> Testimonials => Set<Testimonial>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductLog> ProductLogs => Set<ProductLog>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<OrderNote> OrderNotes => Set<OrderNote>();
    public DbSet<UserAccountLog> UserAccountLogs => Set<UserAccountLog>();
    
    // RBAC
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRoleMapping> UserRoleMappings => Set<UserRoleMapping>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RbacAuditLog> RbacAuditLogs => Set<RbacAuditLog>();

    // Chat / RAG
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Category - Self-referencing
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasOne(c => c.Parent)
                  .WithMany(c => c.Children)
                  .HasForeignKey(c => c.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Product
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasOne(p => p.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Product - Tag (Many-to-Many)
        modelBuilder.Entity<Product>()
            .HasMany(p => p.Tags)
            .WithMany(t => t.Products)
            .UsingEntity(j => j.ToTable("ProductTagMapping"));

        // Address
        modelBuilder.Entity<Address>(entity =>
        {
            entity.Property(a => a.GhnWardCode)
                  .HasMaxLength(20);
        });

        // Cart - User (One-to-One)
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasOne(c => c.User)
                  .WithOne(u => u.Cart)
                  .HasForeignKey<Cart>(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Order
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.CreatedAt }); // Index cho lịch sử đơn hàng
            entity.HasIndex(e => e.Status); // Index cho lọc theo trạng thái
            entity.HasIndex(e => e.AddressId);
            entity.HasIndex(e => e.PaymentCode)
                  .IsUnique()
                  .HasFilter("[PaymentCode] IS NOT NULL");
            entity.HasOne(o => o.Address)
                  .WithMany(a => a.Orders)
                  .HasForeignKey(o => o.AddressId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SePayTransaction>(entity =>
        {
            entity.HasIndex(e => e.SePayTransactionId).IsUnique();
            entity.HasIndex(e => e.PaymentCode);
            entity.HasIndex(e => e.OrderId);
            entity.HasOne(e => e.Order)
                  .WithMany()
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Wishlist - Unique constraint
        modelBuilder.Entity<Wishlist>(entity =>
        {
            entity.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();
        });

        // Coupon
        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // Setting
        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // ProductVariant
        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasIndex(e => e.SKU).IsUnique();
            entity.HasOne(v => v.Product)
                  .WithMany(p => p.Variants)
                  .HasForeignKey(v => v.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ProductLog
        modelBuilder.Entity<ProductLog>(entity =>
        {
            entity.HasOne(l => l.Product)
                  .WithMany()
                  .HasForeignKey(l => l.ProductId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(l => l.Admin)
                  .WithMany()
                  .HasForeignKey(l => l.AdminId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedAt);
        });

        // OrderStatusHistory
        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasOne(h => h.Order)
                  .WithMany(o => o.StatusHistory)
                  .HasForeignKey(h => h.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(h => h.Admin)
                  .WithMany()
                  .HasForeignKey(h => h.AdminId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // OrderNote
        modelBuilder.Entity<OrderNote>(entity =>
        {
            entity.HasOne(n => n.Order)
                  .WithMany(o => o.OrderNotes)
                  .HasForeignKey(n => n.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // UserAccountLog
        modelBuilder.Entity<UserAccountLog>(entity =>
        {
            entity.HasOne(l => l.User)
                  .WithMany(u => u.AccountLogs)
                  .HasForeignKey(l => l.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(l => l.Admin)
                  .WithMany()
                  .HasForeignKey(l => l.AdminId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.AdminId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // User - LockedByAdmin relationship
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasOne(u => u.LockedByAdmin)
                  .WithMany()
                  .HasForeignKey(u => u.LockedByAdminId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Permission
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Module);
        });

        // UserRoleMapping
        modelBuilder.Entity<UserRoleMapping>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.RoleId);
            entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();
            
            entity.HasOne(ur => ur.User)
                  .WithMany(u => u.UserRoleMappings)
                  .HasForeignKey(ur => ur.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(ur => ur.Role)
                  .WithMany(r => r.UserRoleMappings)
                  .HasForeignKey(ur => ur.RoleId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(ur => ur.AssignedByAdmin)
                  .WithMany()
                  .HasForeignKey(ur => ur.AssignedByAdminId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // RolePermission
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasIndex(e => e.RoleId);
            entity.HasIndex(e => e.PermissionId);
            entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();
            
            entity.HasOne(rp => rp.Role)
                  .WithMany(r => r.RolePermissions)
                  .HasForeignKey(rp => rp.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(rp => rp.Permission)
                  .WithMany(p => p.RolePermissions)
                  .HasForeignKey(rp => rp.PermissionId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(rp => rp.AssignedByAdmin)
                  .WithMany()
                  .HasForeignKey(rp => rp.AssignedByAdminId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // RbacAuditLog
        modelBuilder.Entity<RbacAuditLog>(entity =>
        {
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => e.ChangedAt);
            entity.HasIndex(e => e.ChangedByAdminId);
            
            entity.HasOne(a => a.ChangedByAdmin)
                  .WithMany()
                  .HasForeignKey(a => a.ChangedByAdminId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Review
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.Status, e.IsHidden, e.IsDeleted });
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Rating);
            entity.HasIndex(e => new { e.ProductId, e.Status, e.IsHidden })
                  .HasFilter("[IsDeleted] = 0");
            
            entity.HasOne(r => r.Product)
                  .WithMany(p => p.Reviews)
                  .HasForeignKey(r => r.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(r => r.User)
                  .WithMany(u => u.Reviews)
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .IsRequired();
            
            entity.HasOne(r => r.HiddenByAdmin)
                  .WithMany()
                  .HasForeignKey(r => r.HiddenByAdminId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .IsRequired(false);
            
            entity.HasOne(r => r.DeletedByAdmin)
                  .WithMany()
                  .HasForeignKey(r => r.DeletedByAdminId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .IsRequired(false);
        });

        // ReviewReport
        modelBuilder.Entity<ReviewReport>(entity =>
        {
            entity.HasIndex(e => e.ReviewId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.ReportedByUserId, e.ReviewId }).IsUnique();
            
            entity.HasOne(rr => rr.Review)
                  .WithMany(r => r.Reports)
                  .HasForeignKey(rr => rr.ReviewId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(rr => rr.ReportedByUser)
                  .WithMany()
                  .HasForeignKey(rr => rr.ReportedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(rr => rr.HandledByAdmin)
                  .WithMany()
                  .HasForeignKey(rr => rr.HandledByAdminId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ReviewHelpful - Unique per user+review
        modelBuilder.Entity<ReviewHelpful>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ReviewId }).IsUnique();
            entity.HasOne(h => h.Review)
                  .WithMany(r => r.HelpfulVotes)
                  .HasForeignKey(h => h.ReviewId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(h => h.User)
                  .WithMany()
                  .HasForeignKey(h => h.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ChatSession
        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.LastMessageAt);
        });

        // ChatMessage
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasOne(e => e.Session)
                  .WithMany(s => s.Messages)
                  .HasForeignKey(e => e.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.SessionId);
        });

        // KnowledgeChunk
        modelBuilder.Entity<KnowledgeChunk>(entity =>
        {
            entity.HasIndex(e => new { e.SourceType, e.SourceId });
            entity.HasIndex(e => e.IsActive);
        });

        // Faq
        modelBuilder.Entity<Faq>(entity =>
        {
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.Category);
        });

        // Seed starter FAQs for chat RAG (fixed IDs for HasData)
        var faqSeedTime = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<Faq>().HasData(
            new Faq
            {
                Id = 1,
                Title = "Phí vận chuyển như thế nào?",
                Body = "Phí vận chuyển được tính theo khu vực: nội thành (zone 1), các tỉnh lân cận (zone 2) và các tỉnh xa (zone 3). Đơn hàng đạt ngưỡng miễn phí ship sẽ được miễn phí vận chuyển. Chi tiết phí hiển thị khi bạn chọn địa chỉ giao hàng ở bước thanh toán.",
                Category = "shipping",
                IsActive = true,
                CreatedAt = faqSeedTime,
                UpdatedAt = faqSeedTime
            },
            new Faq
            {
                Id = 2,
                Title = "Thanh toán bằng cách nào?",
                Body = "Fruitables hỗ trợ thanh toán qua SePay QR khi checkout. Sau khi đặt hàng, bạn quét mã QR để chuyển khoản; hệ thống tự xác nhận thanh toán khi nhận được giao dịch.",
                Category = "payment",
                IsActive = true,
                CreatedAt = faqSeedTime,
                UpdatedAt = faqSeedTime
            },
            new Faq
            {
                Id = 3,
                Title = "Bảo quản rau củ tươi như thế nào?",
                Body = "Rau củ tươi nên bảo quản trong tủ lạnh (ngăn mát), để trong túi hoặc hộp thoáng khí, tránh để gần trái cây chín. Dùng sớm trong vài ngày để giữ độ tươi ngon tốt nhất.",
                Category = "product-care",
                IsActive = true,
                CreatedAt = faqSeedTime,
                UpdatedAt = faqSeedTime
            },
            new Faq
            {
                Id = 4,
                Title = "Giờ làm việc và liên hệ?",
                Body = "Bạn có thể xem giờ làm việc và thông tin liên hệ (điện thoại, email, địa chỉ) trên trang Liên hệ hoặc phần chân trang website. Chúng tôi sẵn sàng hỗ trợ trong khung giờ làm việc đã công bố.",
                Category = "hours",
                IsActive = true,
                CreatedAt = faqSeedTime,
                UpdatedAt = faqSeedTime
            },
            new Faq
            {
                Id = 5,
                Title = "Làm sao để kiểm tra đơn hàng?",
                Body = "Đăng nhập tài khoản, vào mục Lịch sử đơn hàng để xem trạng thái, chi tiết và theo dõi đơn. Bạn cần đăng nhập để xem các đơn gắn với tài khoản của mình.",
                Category = "order",
                IsActive = true,
                CreatedAt = faqSeedTime,
                UpdatedAt = faqSeedTime
            },
            new Faq
            {
                Id = 6,
                Title = "Chính sách đổi trả như thế nào?",
                Body = "Nếu sản phẩm bị lỗi hoặc không đúng mô tả, vui lòng liên hệ CSKH trong vòng 24 giờ kể từ khi nhận hàng để được hỗ trợ đổi trả. Giữ nguyên bao bì và chụp ảnh minh chứng nếu có.",
                Category = "return",
                IsActive = true,
                CreatedAt = faqSeedTime,
                UpdatedAt = faqSeedTime
            }
        );

        // Seed Admin User
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Name = "Admin User",
                Email = "admin@fruitables.com",
                Password = AdminPasswordHash,
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = 2,
                Name = "Super Admin",
                Email = "superadmin@fruitables.com",
                Password = AdminPasswordHash,
                Role = UserRole.SuperAdmin,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

    }

    /// <summary>
    /// Seeds default settings if they don't exist (from original HTML template)
    /// </summary>
    public async Task SeedDefaultSettingsAsync()
    {
        var defaultSettings = new Dictionary<string, (string Value, string Group)>
        {
            // General Settings
            ["site_name"] = ("Fruitables", "General"),
            
            // SEO Settings
            ["meta_title"] = ("Fruitables - Vegetable Website Template", "SEO"),
            ["meta_description"] = ("Fresh organic vegetables and fruits delivered to your door", "SEO"),
            ["meta_keywords"] = ("organic, vegetables, fruits, fresh, healthy, food", "SEO"),
            
            // Contact Settings
            ["contact_address"] = ("1429 Netus Rd, NY 48247", "Contact"),
            ["contact_phone"] = ("+0123 4567 8910", "Contact"),
            ["contact_email"] = ("info@fruitables.com", "Contact"),
            ["contact_working_hours"] = ("Mon - Sat: 8:00 - 18:00", "Contact"),
            ["contact_map_embed"] = (@"<iframe class=""rounded w-100"" style=""height: 400px;"" src=""https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d387191.33750346623!2d-73.97968099999999!3d40.6974881!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x89c24fa5d33f083b%3A0xc80b8f06e177fe62!2sNew%20York%2C%20NY%2C%20USA!5e0!3m2!1sen!2sbd!4v1694259649153!5m2!1sen!2sbd"" loading=""lazy"" referrerpolicy=""no-referrer-when-downgrade""></iframe>", "Contact"),
            
            // Social Settings
            ["social_facebook"] = ("https://facebook.com/fruitables", "Social"),
            ["social_twitter"] = ("https://twitter.com/fruitables", "Social"),
            ["social_instagram"] = ("https://instagram.com/fruitables", "Social"),
            ["social_youtube"] = ("https://youtube.com/fruitables", "Social"),
            ["social_linkedin"] = ("https://linkedin.com/company/fruitables", "Social")
        };

        // Get existing keys
        var existingKeys = await Settings.Select(s => s.Key).ToListAsync();
        
        // Add missing settings
        var settingsToAdd = defaultSettings
            .Where(kv => !existingKeys.Contains(kv.Key))
            .Select(kv => new Setting { Key = kv.Key, Value = kv.Value.Value, Group = kv.Value.Group })
            .ToList();

        if (settingsToAdd.Count > 0)
        {
            Settings.AddRange(settingsToAdd);
            await SaveChangesAsync();
        }
    }
}
