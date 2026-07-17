# Thiết kế: Upload ảnh combo thay vì nhập URL

## Ngữ cảnh

Tính năng Combo món ăn hiện tại yêu cầu admin nhập URL ảnh vào ô text. Yêu cầu mới: thay ô nhập URL bằng input upload file trực tiếp, đơn giản hơn cho ngườiquản trị.

## Quyết định thiết kế

- **Phạm vi:** Chỉ thay đổi cách nhập ảnh trong admin CRUD combo. Cấu trúc DB (`Combo.ImageUrl`) không đổi.
- **Storage:** Lưu file vật lý vào `wwwroot/uploads/combos/` và ghi đường dẫn tương đối (`/uploads/combos/{guid}.ext`) vào `ImageUrl`.
- **Pattern:** Dùng lại `IImageUploadService` đã có trong codebase, xử lý upload trong controller (không đưa xuống service để giữ đơn giản).
- **URL fallback:** Không giữ lại ô nhập URL. Chỉ dùng upload file.

## Thay đổi chi tiết

### 1. ViewModel

File: `ViewModels/ComboViewModels.cs`

Thêm vào `ComboFormViewModel`:

```csharp
[Display(Name = "Hình ảnh")]
[DataType(DataType.Upload)]
public IFormFile? ImageFile { get; set; }
```

`ImageUrl` vẫn giữ để hiển thị ảnh đã upload và lưu DB.

### 2. Controller

File: `Areas/Admin/Controllers/ComboController.cs`

- Inject `IImageUploadService` qua constructor.
- Tạo helper `TryUploadComboImageAsync`:
  - Nhận `IFormFile?`.
  - Kiểm tra `IsValidImageFile` và `IsValidFileSize`.
  - Gọi `UploadImageAsync(file, "combos")`.
  - Trả về `string? imageUrl` hoặc thêm lỗi vào `ModelState`.

#### Create

```csharp
public async Task<IActionResult> Create(ComboFormViewModel model)
{
    if (model.ImageFile != null)
    {
        model.ImageUrl = await TryUploadComboImageAsync(model.ImageFile);
        if (model.ImageUrl == null) return View(model);
    }

    var result = await _comboService.CreateAsync(model);
    ...
}
```

#### Edit

```csharp
public async Task<IActionResult> Edit(int id, ComboFormViewModel model)
{
    if (model.ImageFile != null)
    {
        var existing = await _comboService.GetForEditAsync(id);
        if (!string.IsNullOrEmpty(existing?.ImageUrl))
            await _imageUploadService.DeleteImageAsync(existing.ImageUrl);

        model.ImageUrl = await TryUploadComboImageAsync(model.ImageFile);
        if (model.ImageUrl == null) return View(model);
    }

    var result = await _comboService.UpdateAsync(id, model);
    ...
}
```

### 3. View

File: `Areas/Admin/Views/Combo/_ComboForm.cshtml`

- Thêm `enctype="multipart/form-data"` cho form.
- Thay phần input `ImageUrl` bằng:

```html
<div class="col-md-6 mb-3">
    <label asp-for="ImageFile" class="form-label">Hình ảnh</label>
    <input asp-for="ImageFile" class="form-control" type="file" accept="image/*" />
    <span asp-validation-for="ImageFile" class="text-danger"></span>
    @if (!string.IsNullOrEmpty(Model.ImageUrl))
    {
        <div class="mt-2">
            <img src="@Model.ImageUrl" alt="Current combo image" style="max-height: 120px;" class="img-thumbnail" />
            <small class="text-muted d-block">Upload ảnh mới để thay thế</small>
        </div>
    }
</div>
```

- Xóa input text `ImageUrl`.

### 4. Validation

Dùng lại quy tắc của `ImageUploadService`:
- Định dạng: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`.
- Content type tương ứng.
- Max size: 5MB.

Nếu validation fail, thêm `ModelState.AddModelError("ImageFile", message)` và trả về view.

### 5. Xử lý lỗi

- File không hợp lệ: báo lỗi cụ thể, giữ nguyên các trường đã nhập.
- Upload thất bại (disk error, etc.): báo lỗi chung.
- Edit thay ảnh: xóa ảnh cũ trước khi upload mới. Nếu upload mới fail, ảnh cũ đã mất → đây là trade-off chấp nhận được vì form edit có preview rõ ràng.

## Không thay đổi

- `ComboService`, `IComboService`: không thay đổi signature.
- Entity `Combo` và migration: không thay đổi.
- Storefront `_ComboSection.cshtml`: vẫn dùng `ImageUrl` như cũ.

## Kiểm thử

1. Admin tạo combo mới kèm upload ảnh → ảnh xuất hiện trong preview sau khi lưu.
2. Admin edit combo, upload ảnh mới → ảnh cũ bị xóa, ảnh mới hiển thị.
3. Storefront `/Shop` hiển thị combo với ảnh đã upload.
4. Upload file không hợp lệ → báo lỗi, không lưu combo.
