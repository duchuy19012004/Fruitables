using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    
    // Existing repositories
    private ICategoryRepository? _categories;
    private IRepository<Product>? _products;
    private IRepository<Order>? _orders;
    private IRepository<OrderItem>? _orderItems;
    private IRepository<Setting>? _settings;
    private IRepository<Review>? _reviews;
    private IReviewRepository? _reviewRepository;
    private IReviewReportRepository? _reviewReports;
    private IRepository<ReviewHelpful>? _reviewHelpfuls;
    private IRepository<ReviewSentiment>? _reviewSentiments;
    private IRepository<ReviewSentimentAspect>? _reviewSentimentAspects;
    private IRepository<ContactMessage>? _contacts;
    private IRepository<Testimonial>? _testimonials;
    private IRepository<User>? _users;
    
    // New repositories
    private IRepository<Cart>? _carts;
    private IRepository<CartGroup>? _cartGroups;
    private IRepository<CartItem>? _cartItems;
    private IRepository<ProductImage>? _productImages;
    private IRepository<ProductVariant>? _productVariants;
    private IRepository<ProductTag>? _productTags;
    private IRepository<ProductLog>? _productLogs;
    private IRepository<PriceSchedule>? _priceSchedules;
    private IRepository<Combo>? _combos;
    private IRepository<ComboItem>? _comboItems;
    private IRepository<ComboAuditLog>? _comboAuditLogs;
    private IRepository<Address>? _addresses;
    
    // RBAC repositories
    private IRepository<Role>? _roles;
    private IRepository<Permission>? _permissions;
    private IRepository<UserRoleMapping>? _userRoleMappings;
    private IRepository<RolePermission>? _rolePermissions;
    private IRepository<RbacAuditLog>? _rbacAuditLogs;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public ApplicationDbContext Context => _context;

    // Existing repository properties
    public ICategoryRepository Categories => 
        _categories ??= new CategoryRepository(_context);
    
    public IRepository<Product> Products => 
        _products ??= new Repository<Product>(_context);
    
    public IRepository<Order> Orders => 
        _orders ??= new Repository<Order>(_context);
    
    public IRepository<OrderItem> OrderItems => 
        _orderItems ??= new Repository<OrderItem>(_context);
    
    public IRepository<Setting> Settings => 
        _settings ??= new Repository<Setting>(_context);
    
    public IRepository<Review> Reviews => 
        _reviews ??= new Repository<Review>(_context);
    
    public IReviewRepository ReviewRepository => 
        _reviewRepository ??= new ReviewRepository(_context);
    
    public IReviewReportRepository ReviewReports => 
        _reviewReports ??= new ReviewReportRepository(_context);
    
    public IRepository<ReviewHelpful> ReviewHelpfuls => 
        _reviewHelpfuls ??= new Repository<ReviewHelpful>(_context);
    
    public IRepository<ReviewSentiment> ReviewSentiments =>
        _reviewSentiments ??= new Repository<ReviewSentiment>(_context);

    public IRepository<ReviewSentimentAspect> ReviewSentimentAspects =>
        _reviewSentimentAspects ??= new Repository<ReviewSentimentAspect>(_context);
    
    public IRepository<ContactMessage> Contacts => 
        _contacts ??= new Repository<ContactMessage>(_context);
    
    public IRepository<Testimonial> Testimonials => 
        _testimonials ??= new Repository<Testimonial>(_context);
    
    public IRepository<User> Users => 
        _users ??= new Repository<User>(_context);
    
    // New repository properties
    public IRepository<Cart> Carts => 
        _carts ??= new Repository<Cart>(_context);

    public IRepository<CartGroup> CartGroups =>
        _cartGroups ??= new Repository<CartGroup>(_context);
    
    public IRepository<CartItem> CartItems => 
        _cartItems ??= new Repository<CartItem>(_context);
    
    public IRepository<ProductImage> ProductImages => 
        _productImages ??= new Repository<ProductImage>(_context);
    
    public IRepository<ProductVariant> ProductVariants => 
        _productVariants ??= new Repository<ProductVariant>(_context);
    
    public IRepository<ProductTag> ProductTags => 
        _productTags ??= new Repository<ProductTag>(_context);
    
    public IRepository<ProductLog> ProductLogs => 
        _productLogs ??= new Repository<ProductLog>(_context);

    public IRepository<PriceSchedule> PriceSchedules =>
        _priceSchedules ??= new Repository<PriceSchedule>(_context);

    public IRepository<Combo> Combos =>
        _combos ??= new Repository<Combo>(_context);

    public IRepository<ComboItem> ComboItems =>
        _comboItems ??= new Repository<ComboItem>(_context);

    public IRepository<ComboAuditLog> ComboAuditLogs =>
        _comboAuditLogs ??= new Repository<ComboAuditLog>(_context);
    
    public IRepository<Address> Addresses =>
        _addresses ??= new Repository<Address>(_context);
    
    // Coupon repository
    private IRepository<Coupon>? _coupons;
    public IRepository<Coupon> Coupons =>
        _coupons ??= new Repository<Coupon>(_context);
    
    // RBAC repository properties
    public IRepository<Role> Roles => 
        _roles ??= new Repository<Role>(_context);
    
    public IRepository<Permission> Permissions => 
        _permissions ??= new Repository<Permission>(_context);
    
    public IRepository<UserRoleMapping> UserRoleMappings => 
        _userRoleMappings ??= new Repository<UserRoleMapping>(_context);
    
    public IRepository<RolePermission> RolePermissions => 
        _rolePermissions ??= new Repository<RolePermission>(_context);
    
    public IRepository<RbacAuditLog> RbacAuditLogs => 
        _rbacAuditLogs ??= new Repository<RbacAuditLog>(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction BeginTransaction()
    {
        return _context.Database.BeginTransaction();
    }

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync()
    {
        return _context.Database.BeginTransactionAsync();
    }

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel)
    {
        return _context.Database.BeginTransactionAsync(isolationLevel);
    }

    public string? DatabaseProviderName => _context.Database.ProviderName;

    public void Dispose()
    {
        _context.Dispose();
    }
}
