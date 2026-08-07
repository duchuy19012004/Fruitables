# Fruitables - Hệ Thống Thương Mại Điện Tử Rau Củ Quả

## Giới Thiệu

Fruitables là một hệ thống thương mại điện tử chuyên về bán rau củ quả tươi sạch, được xây dựng trên nền tảng ASP.NET Core 8.0 với kiến trúc MVC. Hệ thống cung cấp đầy đủ các tính năng cho cả khách hàng và quản trị viên, từ mua sắm trực tuyến, quản lý đơn hàng đến thống kê doanh thu chi tiết.

## Công Nghệ Sử Dụng

### Backend
- **Framework**: ASP.NET Core 8.0 (MVC Pattern)
- **Database**: SQL Server với Entity Framework Core 8.0
- **Authentication**: 
  - Cookie-based Authentication
  - Google OAuth 2.0
  - BCrypt.Net cho mã hóa mật khẩu
  - **RBAC (Role-Based Access Control)** với fine-grained permissions
- **ORM**: Entity Framework Core với Code-First Migrations
- **Dependency Injection**: Built-in ASP.NET Core DI Container
- **Caching**: Memory Cache cho performance optimization

### Frontend
- **Template Engine**: Razor Views (.cshtml)
- **CSS Framework**: Bootstrap 5
- **JavaScript**: Vanilla JS, jQuery
- **UI Components**: Custom components với ViewComponents

### Libraries & Tools
- **ClosedXML**: Xuất báo cáo Excel
- **Memory Cache**: Caching dữ liệu thường xuyên truy cập
- **HttpClient**: Tích hợp API địa chỉ Việt Nam

## Kiến Trúc Hệ Thống

### Layered Architecture

```
┌─────────────────────────────────────────┐
│         Presentation Layer              │
│  (Controllers, Views, ViewComponents)   │
├─────────────────────────────────────────┤
│          Service Layer                  │
│    (Business Logic Services)            │
├─────────────────────────────────────────┤
│        Repository Layer                 │
│   (Data Access, Unit of Work)           │
├─────────────────────────────────────────┤
│          Data Layer                     │
│  (Entity Framework, SQL Server)         │
└─────────────────────────────────────────┘
```

### Design Patterns
- **Repository Pattern**: Trừu tượng hóa data access
- **Unit of Work Pattern**: Quản lý transactions
- **Dependency Injection**: Loose coupling giữa các components
- **Service Layer Pattern**: Tách biệt business logic
- **ViewComponent Pattern**: Tái sử dụng UI components

## Cấu Trúc Dự Án
```
Fruitables/
│
├── Areas/
│   └── Admin/                    # Admin Panel (Dashboard, Quản lý)
│       ├── Controllers/          # Admin controllers
│       └── Views/                # Admin views
│
├── Controllers/                  # Public controllers
├── Services/                     # Business logic services
│   └── Interfaces/               # Service interfaces
│
├── Repositories/                 # Data access layer
│   └── Interfaces/               # Repository interfaces
│
├── Models/                       # Domain models & entities
├── ViewModels/                   # Data transfer objects cho views
├── Data/                         # DbContext & migrations
├── Helpers/                      # Utility classes
├── Constants/                    # Application constants
├── ViewComponents/               # Reusable UI components
├── Views/                        # Razor views
├── wwwroot/                      # Static files (CSS, JS, images)
└── Migrations/                   # EF Core migrations

Fruitables.Tests/                 # Test project
└── *Tests.cs                     # Unit & property tests
```
## Modules Chính

### 1. Module Xác Thực & Phân Quyền (RBAC)
**Trạng thái**: ✅ Hoàn thành

**Chức năng**:
- Đăng ký, đăng nhập với email/password
- Đăng nhập với Google OAuth
- **Quên mật khẩu & Reset password**:
  - Gửi email với link reset password
  - Token có thời hạn (configurable)
  - Xác thực token trước khi reset
  - Mã hóa password mới với BCrypt
- **Hệ thống RBAC (Role-Based Access Control)**:
  - Quản lý Roles (vai trò) động
  - Quản lý Permissions (quyền) chi tiết
  - Gán permissions cho roles
  - Gán roles cho users
  - Audit log đầy đủ cho mọi thay đổi
  - Migration từ hệ thống enum cũ sang RBAC
  - Backward compatibility với legacy code
- Phân quyền: Customer, Admin, SuperAdmin (có thể mở rộng)
- Quản lý session và cookie authentication
- Khóa/mở khóa tài khoản người dùng
- **Diagnostics tools** để troubleshoot permission issues

**Services**:
- `AuthenticationService`: Xác thực cơ bản
- `GoogleAuthService`: Tích hợp Google OAuth
- `UserAuthService`: Quản lý người dùng
- `UserManagementService`: Quản lý tài khoản (khóa/mở)
- **`RbacService`**: Quản lý roles và permissions
- **`MigrationService`**: Migration từ legacy sang RBAC

**Models**:
- **`Role`**: Vai trò trong hệ thống
- **`Permission`**: Quyền cụ thể (products.create, orders.view, etc.)
- **`UserRoleMapping`**: Mapping user-role
- **`RolePermission`**: Mapping role-permission
- **`RbacAuditLog`**: Audit log cho RBAC

**Controllers**:
- **`RoleController`**: Quản lý roles
- **`PermissionController`**: Quản lý permissions
- **`RbacAuditController`**: Xem audit logs
- **`DiagnosticsController`**: Troubleshooting tools

**Permissions mặc định**:
- **Products**: view, create, update, delete, manage_inventory
- **Orders**: view_all, view_own, create, update_status, cancel, refund
- **Users**: view, create, update, lock, unlock, delete
- **Reviews**: view, create, moderate, delete
- **Settings**: view, update
- **Dashboard**: view, view_statistics
- **System**: manage, view_logs, manage_rbac

### 2. Module Quản Lý Sản Phẩm
**Trạng thái**: ✅ Hoàn thành

**Chức năng**:
- CRUD sản phẩm với nhiều hình ảnh
- Quản lý danh mục (categories) với cấu trúc cây
- Quản lý biến thể sản phẩm (variants) với SKU
- Quản lý tags cho sản phẩm
- Lịch sử thay đổi sản phẩm (audit log)
- Tìm kiếm và lọc sản phẩm

**Services**:
- `ProductService`: Hiển thị sản phẩm cho khách hàng
- `ProductAdminService`: Quản lý sản phẩm (CRUD)
- `CategoryService`: Quản lý danh mục
- `ProductLogService`: Ghi log thay đổi
- `ImageUploadService`: Upload và quản lý hình ảnh

### 3. Module Giỏ Hàng & Thanh Toán
**Trạng thái**: ✅ Hoàn thành

**Chức năng**:
- Giỏ hàng với session storage
- Tính toán tự động phí vận chuyển theo vùng
- Quản lý địa chỉ giao hàng (tích hợp API địa chỉ VN)
- Snapshot địa chỉ và phí ship khi đặt hàng
- Áp dụng mã giảm giá (coupons)
- Xác nhận đơn hàng

**Services**:
- `CartService`: Quản lý giỏ hàng
- `ShippingService`: Tính phí vận chuyển
- `AddressService`: Quản lý địa chỉ
- `VietnamAddressService`: Tích hợp API địa chỉ VN
- `OrderService`: Xử lý đơn hàng

### 4. Module Quản Lý Đơn Hàng
**Trạng thái**: ✅ Hoàn thành

**Chức năng**:
- Theo dõi trạng thái đơn hàng (Pending → Processing → Shipped → Delivered)
- Quản lý trạng thái thanh toán (Unpaid → Paid → Refunded)
- Hủy đơn hàng với lý do và hoàn kho tự động
- Lịch sử thay đổi trạng thái với audit log
- Đính kèm file (attachments) cho đơn hàng
- Xử lý concurrency với RowVersion
- Lọc và tìm kiếm đơn hàng

**Services**:
- `OrderService`: Xử lý đơn hàng khách hàng
- `OrderAdminService`: Quản lý đơn hàng (Admin)
- `OrderHistoryService`: Lịch sử đơn hàng
- `OrderLogService`: Audit logging

**Models**:
- `Order`: Thông tin đơn hàng
- `OrderStatusHistory`: Lịch sử trạng thái
- `OrderStatusAuditLog`: Chi tiết audit log
- `AuditLogAttachment`: File đính kèm

### 5. Module Thống Kê Doanh Thu
**Trạng thái**: ✅ Hoàn thành

**Chức năng**:
- Thống kê doanh thu thuần (Net Revenue)
- Lọc theo khoảng thời gian với presets
- Doanh thu theo danh mục sản phẩm
- Top sản phẩm bán chạy
- Biểu đồ xu hướng (daily/weekly/monthly)
- So sánh giữa các kỳ
- Xuất báo cáo Excel
- Thống kê đơn hàng bị hủy với lý do

**Services**:
- `RevenueStatisticsService`: Thống kê doanh thu
- `CancelledOrdersStatisticsService`: Thống kê đơn hủy
- `DashboardService`: Tổng quan dashboard

**ViewModels**:
- `RevenueViewModel`: Dữ liệu doanh thu
- `CancelledOrdersViewModel`: Dữ liệu đơn hủy

### 6. Module Quản Lý Người Dùng
**Trạng thái**: ✅ Hoàn thành

**Chức năng**:
- Xem danh sách khách hàng với phân trang
- Xem chi tiết khách hàng và lịch sử mua hàng
- Khóa/mở khóa tài khoản (Temporary/Permanent)
- Phân loại vi phạm và ghi lý do
- Audit log cho các hành động quản lý
- Phân quyền Admin/SuperAdmin
- Gửi email thông báo khóa/mở khóa

**Services**:
- `UserManagementService`: Quản lý người dùng
- `EmailService`: Gửi email thông báo

**Models**:
- `User`: Thông tin người dùng
- `UserAccountLog`: Lịch sử khóa/mở
- `LockType`: Enum loại khóa
- `ViolationTypes`: Enum loại vi phạm

### 7. Module Đánh Giá Sản Phẩm (Product Comments)
**Trạng thái**: ✅ Hoàn thành

**Chức năng Customer**:
- Đánh giá sản phẩm với rating (1-5 sao) và comment
- Chỉnh sửa đánh giá trong 24h sau khi tạo
- Xóa đánh giá của chính mình
- Đánh dấu đánh giá hữu ích (helpful)
- Báo cáo đánh giá vi phạm với lý do cụ thể
- Badge "Đã mua hàng" cho verified purchase
- Giới hạn 5 đánh giá/ngày/user (rate limiting)
- Lọc từ ngữ không phù hợp tự động

**Chức năng Admin**:
- Xem tất cả đánh giá với lọc và tìm kiếm
- Ẩn/hiện đánh giá với lý do
- Xóa đánh giá vi phạm (soft delete)
- Quản lý báo cáo vi phạm (resolve/dismiss)
- Thống kê đánh giá:
  - Tổng quan (total, approved, pending, hidden)
  - Phân bố rating (1-5 sao)
  - Top sản phẩm được đánh giá cao
  - Sản phẩm có nhiều đánh giá nhất
- Audit logging đầy đủ cho mọi hành động admin

**Services**:
- `ReviewService`: Quản lý đánh giá (CRUD, moderation, statistics)
- `WordMaskingService`: Lọc từ ngữ không phù hợp
- `TestimonialService`: Quản lý testimonials

**Models**:
- `Review`: Đánh giá sản phẩm với status, visibility
- `ReviewReport`: Báo cáo vi phạm
- `ReviewStatus`: Enum (Pending/Approved/Rejected)
- `ReportReason`: Enum (Spam/Offensive/Fake/Other)
- `ReportStatus`: Enum (Pending/Resolved/Dismissed)

**ViewModels**:
- `ReviewViewModel`: Hiển thị đánh giá cho customer
- `ReviewAdminViewModel`: Hiển thị đánh giá cho admin
- `ReviewStatistics`: Thống kê tổng quan
- `ReviewAdminStatistics`: Thống kê chi tiết cho admin
- `CreateReviewDto`, `UpdateReviewDto`, `ReportReviewDto`

**Controllers**:
- `ReviewController`: API cho customer operations
- `ReviewAdminController`: Quản lý đánh giá (admin panel)

**Repositories**:
- `ReviewRepository`: Data access cho reviews
- `ReviewReportRepository`: Data access cho reports

**RBAC Permissions**:
- `reviews.view`: Xem đánh giá
- `reviews.create`: Tạo đánh giá
- `reviews.edit_own`: Chỉnh sửa đánh giá của mình
- `reviews.delete_own`: Xóa đánh giá của mình
- `reviews.moderate`: Ẩn/hiện đánh giá (admin)
- `reviews.delete`: Xóa đánh giá (admin)
- `reviews.view_reports`: Xem báo cáo vi phạm
- `reviews.view_statistics`: Xem thống kê

**Security Features**:
- ✅ XSS protection (auto-encode output)
- ✅ CSRF protection (ValidateAntiForgeryToken)
- ✅ SQL injection protection (EF Core parameterized queries)
- ✅ Authorization checks (RBAC + ownership validation)
- ✅ Rate limiting (5 reviews/day/user)
- ✅ Input validation (rating 1-5, comment 10-1000 chars)
- ✅ Audit logging cho admin actions

**API Endpoints**:
- `POST /Review/Create`: Tạo đánh giá
- `PUT /Review/Edit/{id}`: Chỉnh sửa đánh giá
- `DELETE /Review/Delete/{id}`: Xóa đánh giá
- `POST /Review/{id}/Report`: Báo cáo vi phạm
- `POST /Review/{id}/Helpful`: Đánh dấu hữu ích
- `GET /Review/CanReview/{productId}`: Kiểm tra có thể đánh giá
- `GET /Admin/ReviewAdmin`: Quản lý đánh giá
- `POST /Admin/ReviewAdmin/Hide`: Ẩn đánh giá
- `POST /Admin/ReviewAdmin/Show`: Hiện đánh giá
- `POST /Admin/ReviewAdmin/Delete`: Xóa đánh giá
- `GET /Admin/ReviewAdmin/Reports`: Xem báo cáo
- `POST /Admin/ReviewAdmin/HandleReport`: Xử lý báo cáo
- `GET /Admin/ReviewAdmin/Statistics`: Xem thống kê

### 8. Module Cấu Hình Hệ Thống
**Trạng thái**: ✅ Hoàn thành

**Chức năng**:
- Quản lý settings động (key-value)
- Cấu hình thông tin liên hệ
- Cấu hình social media links
- Cấu hình banner và features
- Cấu hình phí vận chuyển theo vùng
- **Cấu hình email settings**:
  - SMTP server configuration
  - Email templates
  - Sender information
- **Cấu hình security settings**:
  - Password reset token expiration
  - Session timeout
  - Rate limiting thresholds
- **Cấu hình application keys**:
  - Google OAuth credentials
  - API keys cho third-party services
  - Encryption keys
- Upload logo và hình ảnh

**Services**:
- `SettingsService`: Quản lý settings
- `ImageUploadService`: Upload hình ảnh

**Models**:
- `Setting`: Key-value settings
- `Shipping`: Cấu hình phí ship

**Constants**:
- `SettingKeys`: Định nghĩa các key chuẩn cho settings

### 9. Module Liên Hệ
**Trạng thái**: ✅ Hoàn thành

**Chức năng**:
- Form liên hệ từ khách hàng
- Lưu trữ tin nhắn liên hệ
- Hiển thị thông tin liên hệ và bản đồ

**Services**:
- `ContactService`: Xử lý liên hệ

**Models**:
- `ContactMessage`: Tin nhắn liên hệ

### 10. Module Profile
**Trạng thái**: ✅ Hoàn thành

**Chức năng**:
- Xem và chỉnh sửa thông tin cá nhân
- Upload avatar
- Quản lý địa chỉ giao hàng
- Xem lịch sử đơn hàng

**Services**:
- `ProfileService`: Quản lý profile

## Modules Sẽ Phát Triển Trong Tương Lai

### 1. Module Nâng Cấp Đánh Giá (Review Enhancements)
**Trạng thái**: 💡 Ý tưởng

**Mô tả**: Nâng cấp module đánh giá với các tính năng nâng cao.

**Chức năng dự kiến**:
- Upload hình ảnh/video trong đánh giá
- Phản hồi đánh giá từ shop owner
- Duyệt/từ chối hàng loạt
- Khôi phục đánh giá đã xóa (trong 30 ngày)
- Gửi email thông báo khi từ chối
- Nâng cao word masking với severity levels
- AI sentiment analysis cho đánh giá
- Gợi ý sản phẩm dựa trên đánh giá


### 2. Module Quản Lý Kho & Tồn Kho
**Trạng thái**: 💡 Ý tưởng

**Chức năng dự kiến**:
- Theo dõi tồn kho theo thời gian thực
- Cảnh báo hết hàng/sắp hết hàng
- Lịch sử nhập/xuất kho
- Quản lý nhà cung cấp
- Báo cáo tồn kho và dự báo nhu cầu
- Tự động đặt hàng khi dưới ngưỡng

### 3. Module Khuyến Mãi & Marketing
**Trạng thái**: 💡 Ý tưởng

**Chức năng dự kiến**:
- Tạo và quản lý mã giảm giá (coupons)
- Flash sale với đếm ngược
- Chương trình tích điểm (loyalty points)
- Email marketing campaigns
- Push notifications
- Khuyến mãi theo nhóm khách hàng

### 4. Module Báo Cáo Nâng Cao
**Trạng thái**: 💡 Ý tưởng

**Chức năng dự kiến**:
- Báo cáo lợi nhuận (revenue - cost)
- Phân tích hành vi khách hàng (RFM analysis)
- Dự báo doanh thu (forecasting)
- Báo cáo hiệu suất sản phẩm
- Dashboard tùy chỉnh
- Xuất báo cáo tự động theo lịch

### 5. Module Chat & Hỗ Trợ Khách Hàng
**Trạng thái**: 💡 Ý tưởng

**Chức năng dự kiến**:
- Live chat với khách hàng
- Chatbot tự động trả lời
- Ticket system cho support
- FAQ management
- Rating hỗ trợ khách hàng

### 6. Module Giao Hàng & Vận Chuyển
**Trạng thái**: 💡 Ý tưởng

**Chức năng dự kiến**:
- Tích hợp API đơn vị vận chuyển (GHN, GHTK, Viettel Post)
- Theo dõi đơn hàng real-time
- In phiếu giao hàng tự động
- Quản lý shipper nội bộ
- Tối ưu tuyến đường giao hàng

### 7. Module Mobile App API
**Trạng thái**: 💡 Ý tưởng

**Chức năng dự kiến**:
- RESTful API cho mobile app
- JWT authentication
- Push notification service
- API documentation với Swagger
- Rate limiting và security

### 8. Module Phân Tích & AI
**Trạng thái**: 💡 Ý tưởng

**Chức năng dự kiến**:
- Gợi ý sản phẩm (recommendation engine)
- Phân loại khách hàng tự động
- Phát hiện gian lận (fraud detection)
- Tối ưu giá (dynamic pricing)
- Sentiment analysis cho đánh giá

## Cài Đặt & Chạy Dự Án

### Yêu Cầu Hệ Thống
- .NET 8.0 SDK
- SQL Server 2019 hoặc mới hơn
- Visual Studio 2022 hoặc VS Code
- Node.js (cho frontend tooling - optional)

### Các Bước Cài Đặt

1. **Clone repository**

```bash
git clone <repository-url>
cd Fruitables
```


2. **Cấu hình Connection String**

Chỉnh sửa `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=FruitablesDb;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

3. **Cấu hình Google OAuth (Optional)**

Thêm vào `appsettings.json` hoặc User Secrets:

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "your-client-id",
      "ClientSecret": "your-client-secret"
    }
  }
}
```

4. **Chạy Migrations**

```bash
cd Fruitables
dotnet ef database update
```

5. **Chạy RBAC Migration (Quan trọng!)**

Sau khi chạy migrations, bạn cần migrate users sang hệ thống RBAC:

**Option 1: Via Web UI (Recommended)**
- Đăng nhập với tài khoản admin
- Truy cập: `https://localhost:5001/Admin/Diagnostics/Migration`
- Nhấn "Run Migration"
- Đăng xuất và đăng nhập lại

**Option 2: Via Diagnostics Page**
- Truy cập: `https://localhost:5001/Admin/Diagnostics`
- Xem system status và làm theo hướng dẫn

6. **Chạy ứng dụng**

```bash
dotnet run
```

7. **Truy cập ứng dụng**
- Frontend: `https://localhost:5001`
- Admin Panel: `https://localhost:5001/Admin`

### Tài Khoản Mặc Định

**Admin**:
- Email: `admin@fruitables.com`
- Password: `Admin@123`

**Super Admin**:
- Email: `superadmin@fruitables.com`
- Password: `Admin@123`

## Chạy Tests

```bash
cd Fruitables.Tests
dotnet test
```

Xem coverage:
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Cấu Trúc Database

### Bảng Chính
- `Users`: Người dùng (Customer/Admin/SuperAdmin)
- **`Roles`**: Vai trò trong hệ thống RBAC
- **`Permissions`**: Quyền cụ thể trong hệ thống
- **`UserRoleMappings`**: Mapping user-role
- **`RolePermissions`**: Mapping role-permission
- **`RbacAuditLogs`**: Audit log cho RBAC
- `Addresses`: Địa chỉ giao hàng
- `Categories`: Danh mục sản phẩm
- `Products`: Sản phẩm
- `ProductImages`: Hình ảnh sản phẩm
- `ProductVariants`: Biến thể sản phẩm
- `ProductTags`: Tags sản phẩm
- `Orders`: Đơn hàng
- `OrderItems`: Chi tiết đơn hàng
- `OrderStatusHistory`: Lịch sử trạng thái
- `OrderStatusAuditLog`: Audit log đơn hàng
- `Reviews`: Đánh giá sản phẩm
- `Carts`: Giỏ hàng
- `CartItems`: Sản phẩm trong giỏ
- `Settings`: Cấu hình hệ thống
- `ContactMessages`: Tin nhắn liên hệ
- `Testimonials`: Testimonials
- `UserAccountLogs`: Log khóa/mở tài khoản
- `ProductLogs`: Log thay đổi sản phẩm

## API Endpoints

### Public APIs
- `GET /`: Trang chủ
- `GET /Shop`: Danh sách sản phẩm
- `GET /Shop/Detail/{id}`: Chi tiết sản phẩm
- `GET /Cart`: Giỏ hàng
- `POST /Cart/Add`: Thêm vào giỏ
- `GET /Checkout`: Thanh toán
- `POST /Checkout/PlaceOrder`: Đặt hàng
- `GET /Account/Login`: Đăng nhập
- `POST /Account/Login`: Xử lý đăng nhập
- `GET /Account/Register`: Đăng ký
- `POST /Account/Register`: Xử lý đăng ký
- **`GET /Account/ForgotPassword`**: Form quên mật khẩu
- **`POST /Account/ForgotPassword`**: Gửi email reset password
- **`GET /Account/ResetPassword`**: Form reset password (với token)
- **`POST /Account/ResetPassword`**: Xử lý reset password

### Admin APIs
- `GET /Admin/Dashboard`: Dashboard
- `GET /Admin/Product`: Quản lý sản phẩm
- `GET /Admin/Order`: Quản lý đơn hàng
- `GET /Admin/User`: Quản lý người dùng
- `GET /Admin/Revenue`: Thống kê doanh thu
- `GET /Admin/Settings`: Cấu hình hệ thống
- **`POST /Admin/Settings/UpdateEmailSettings`**: Cập nhật cấu hình email
- **`POST /Admin/Settings/UpdateSecuritySettings`**: Cập nhật cấu hình bảo mật
- **`POST /Admin/Settings/UpdateApiKeys`**: Cập nhật API keys
- **`GET /Admin/Role`**: Quản lý roles (RBAC)
- **`GET /Admin/Permission`**: Quản lý permissions (RBAC)
- **`GET /Admin/RbacAudit`**: Xem audit logs (RBAC)
- **`GET /Admin/Diagnostics`**: Diagnostics & troubleshooting
- **`GET /Admin/Diagnostics/Migration`**: RBAC migration tool

### Address API
- `GET /api/address/provinces`: Danh sách tỉnh/thành
- `GET /api/address/districts/{provinceId}`: Danh sách quận/huyện
- `GET /api/address/wards/{districtId}`: Danh sách phường/xã

## License

Dự án này được phát triển cho mục đích học tập và thương mại.

**Phiên bản**: 2.2.0  
**Cập nhật lần cuối**: Tháng 2, 2026

---

## 🆕 Changelog

### Version 2.2.0 (Feb 2026)
- ✨ **NEW**: Forgot Password & Reset Password functionality
  - Email-based password reset with secure tokens
  - Configurable token expiration time
  - Token validation and one-time use
  - BCrypt password encryption
- ⚙️ **NEW**: Enhanced Settings Management
  - Email settings configuration (SMTP, templates)
  - Security settings (token expiration, session timeout)
  - API keys management (Google OAuth, third-party services)
  - Encryption keys configuration
- 🔒 **SECURITY**: Improved password reset security with time-limited tokens
- 📧 **EMAIL**: Email service integration for password reset notifications

### Version 2.1.0 (Feb 2026)
- ✨ **NEW**: Complete Product Comments System
  - Customer reviews with rating and comments
  - Edit reviews within 24 hours
  - Report inappropriate reviews
  - Mark reviews as helpful
  - Verified purchase badges
  - Admin moderation panel
  - Review statistics and analytics
  - Rate limiting (5 reviews/day/user)
  - Word masking for inappropriate content
  - Full RBAC integration with 8 review permissions
  - Comprehensive security audit (XSS, CSRF, SQL injection protection)
- 📊 **NEW**: Review statistics dashboard for admins
- 🔒 **SECURITY**: Enhanced input validation and authorization checks
- 🧪 **TESTING**: 60+ test cases (unit, integration, property tests)

### Version 2.0.0 (Feb 2026)
- ✨ **NEW**: Complete RBAC (Role-Based Access Control) system
  - Dynamic role and permission management
  - Fine-grained access control
  - Audit logging for all RBAC operations
  - Migration tool from legacy enum to RBAC
  - Backward compatibility layer
- 🛠️ **NEW**: Diagnostics tools for troubleshooting
- 🔒 **IMPROVED**: Enhanced security with permission-based authorization
- 📚 **DOCS**: Added comprehensive setup guide (SETUP.md)
- 🧹 **CLEANUP**: Removed bin/obj from Git, added proper .gitignore

### Version 1.0.0 (Jan 2026)
- Initial release with core e-commerce features
