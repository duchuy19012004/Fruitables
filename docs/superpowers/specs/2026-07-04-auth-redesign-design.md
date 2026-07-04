# Auth Redesign — Thay thế giao diện đăng nhập / đăng ký

## Tóm tắt
Thay thế giao diện đăng nhập và đăng ký hiện tại của Fruitables bằng giao diện tab duy nhất lấy từ `docs/mockups/auth-redesign.html`. Giao diện mới gộp Login/Register thành một trang với tab chuyển đổi, panel branding bên trái và form bên phải.

## Ngữ cảnh hiện tại
- `Controllers/AccountController.cs` có 2 cặp action GET/POST: `Login` và `Register`.
- `Views/Account/Login.cshtml` và `Views/Account/Register.cshtml` là 2 view riêng, dùng chung `Views/Shared/_AuthLayout.cshtml` (Bootstrap).
- Cả 2 view hiện tại hỗ trợ: form cơ bản, Google OAuth (nếu bật), validation client/server, toast lỗi/thành công.

## Thiết kế

### Kiến trúc
- **Một view chung**: `Views/Account/Auth.cshtml` chứa cả form Đăng nhập và Đăng ký, sử dụng tab chuyển đổi giống mockup.
- **Layout**: view mới sẽ tự chứa toàn bộ cấu trúc HTML của mockup (`Layout = null`) vì mockup là trang full-screen độc lập sử dụng Tailwind CSS.
- **Controller**: giữ nguyên 2 action POST (`Login`, `Register`). Cả 2 action GET đều trả về `Auth.cshtml`, truyền `ViewBag.ActiveTab` = `"login"` hoặc `"register"` để active tab đúng.
- POST thất bại trả về cùng view chung với model tương ứng và tab tương ứng.

### Cấu trúc giao diện
1. **Panel trái (branding)** — ẩn trên mobile, hiển thị từ `md`:
   - Logo Fruitables
   - Slogan: "Thực phẩm sạch, giao tận nhà"
   - 3 selling points với icon
   - Copyright
2. **Panel phải (form)**:
   - Mobile logo
   - Tabs: Đăng nhập / Đăng ký
   - Form Đăng nhập: Email, Mật khẩu, Ghi nhớ đăng nhập, Quên mật khẩu, submit
   - Form Đăng ký: Họ và tên, Email, Mật khẩu, Xác nhận mật khẩu, đồng ý điều khoản, submit
   - Divider "hoặc"
   - Nút "Tiếp tục với Google" (chỉ hiện khi `IsGoogleAuthEnabledAsync()` trả về true)
   - Link "Quay lại trang chủ"
3. **Toast container**: góc trên phải, render server error/success messages.

### Data flow
```
GET /Account/Login
  → AccountController.Login()
  → View("Auth", LoginRequest) + ViewBag.ActiveTab = "login"

GET /Account/Register
  → AccountController.Register()
  → View("Auth", RegisterRequest) + ViewBag.ActiveTab = "register"

POST /Account/Login
  → if ModelState invalid → View("Auth", model) + ActiveTab = "login"
  → if auth fail → ModelState error → View("Auth", model) + ActiveTab = "login"
  → if success → redirect (returnUrl or role-based)

POST /Account/Register
  → if ModelState invalid → View("Auth", model) + ActiveTab = "register"
  → if register fail → ModelState error → View("Auth", model) + ActiveTab = "register"
  → if success → TempData success → RedirectToAction("Login")
```

### Validation & Error Handling
- Client validation qua `asp-validation-for` + `_ValidationScriptsPartial`.
- Server errors (ModelState + TempData) render vào toast container góc trên phải.
- Khi POST thất bại, active tab phải được giữ nguyên để user thấy form vừa submit.
- Preserve `returnUrl` cho login flow.

### Dependencies
- Tailwind CSS via CDN (theo mockup).
- Font Awesome 6 via CDN (theo mockup).
- Google Fonts "Be Vietnam Pro" (đã có trong `_AuthLayout` hiện tại).

### Không thay đổi
- `IUserAuthService`, `IGoogleAuthService` và logic xác thực.
- Cookie/claims flow sau khi đăng nhập thành công.
- Google OAuth callback flow.
- Các action `Logout`, `ForgotPassword`, `ResetPassword`.

### Rủi ro & giảm thiểu
| Rủi ro | Giảm thiểu |
|--------|------------|
| ModelState của cả 2 form lẫn lộn trên cùng 1 view | Mỗi POST chỉ trả về model của chính nó; input name prefix khác nhau |
| Tab không giữ active khi validation fail | Truyền `ViewBag.ActiveTab` từ controller và render class active/tab-hidden động |
| Google button không đúng điều kiện | Giữ `@inject IGoogleAuthService` và `@if (isGoogleEnabled)` giống cũ |

### Acceptance Criteria
- [ ] Trang `/Account/Login` hiển thị giao diện mới với tab Đăng nhập active.
- [ ] Trang `/Account/Register` hiển thị cùng giao diện với tab Đăng ký active.
- [ ] Chuyển tab mượt mà bằng JavaScript.
- [ ] Đăng nhập thành công redirect đúng (returnUrl hoặc theo role).
- [ ] Đăng nhập thất bại hiển thị lỗi và giữ tab Đăng nhập.
- [ ] Đăng ký thành công redirect sang login và hiện thông báo.
- [ ] Đăng ký thất bại hiển thị lỗi và giữ tab Đăng ký.
- [ ] Nút Google chỉ hiện khi Google Auth được bật trong settings.
- [ ] Link "Quay lại trang chủ" hoạt động.
