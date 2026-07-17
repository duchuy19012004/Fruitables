# Combo Image Upload Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the combo image URL text input with a file upload in the admin combo CRUD, storing uploaded images in `wwwroot/uploads/combos/`.

**Architecture:** Add `IFormFile? ImageFile` to the combo form view model. The admin controller validates and uploads the file via the existing `IImageUploadService`, then assigns the returned relative URL to `ImageUrl` before calling `ComboService`. The Razor form uses `enctype="multipart/form-data"` and displays a preview of the current image.

**Tech Stack:** ASP.NET Core 8 MVC, Razor, EF Core, existing `IImageUploadService`.

## Global Constraints

- Use the existing `IImageUploadService` interface and implementation in `Services/ImageUploadService.cs`.
- Uploaded images are stored under `wwwroot/uploads/combos/`.
- Valid image formats: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`.
- Max file size: 5MB (default of `ImageUploadService`).
- Do not change `ComboService` signatures or the `Combo` entity.
- Do not change storefront combo rendering; it continues to read `ImageUrl`.

---

### Task 1: Add ImageFile property to ComboFormViewModel

**Files:**
- Modify: `ViewModels/ComboViewModels.cs:36`

**Interfaces:**
- Consumes: nothing new.
- Produces: `ComboFormViewModel.ImageFile` of type `IFormFile?`.

- [ ] **Step 1: Add using directive**

Add at the top of `ViewModels/ComboViewModels.cs`:

```csharp
using Microsoft.AspNetCore.Http;
```

- [ ] **Step 2: Add ImageFile property**

In `ComboFormViewModel`, add the new property after `ImageUrl`:

```csharp
[Display(Name = "Hình ảnh")]
[DataType(DataType.Upload)]
public IFormFile? ImageFile { get; set; }
```

- [ ] **Step 3: Build**

Run:

```bash
dotnet build
```

Expected: build succeeds, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add ViewModels/ComboViewModels.cs
git commit -m "feat(combo): add ImageFile property to form view model"
```

---

### Task 2: Inject IImageUploadService into Admin ComboController

**Files:**
- Modify: `Areas/Admin/Controllers/ComboController.cs`

**Interfaces:**
- Consumes: `IImageUploadService` from `Fruitables.Services.Interfaces`.
- Produces: `_imageUploadService` field available to Create/Edit actions.

- [ ] **Step 1: Update constructor signature**

Change:

```csharp
public ComboController(IComboService comboService)
{
    _comboService = comboService;
}
```

To:

```csharp
private readonly IImageUploadService _imageUploadService;

public ComboController(IComboService comboService, IImageUploadService imageUploadService)
{
    _comboService = comboService;
    _imageUploadService = imageUploadService;
}
```

- [ ] **Step 2: Build**

Run:

```bash
dotnet build
```

Expected: build succeeds. DI will resolve `IImageUploadService` automatically because it is already registered in `Program.cs`.

- [ ] **Step 3: Commit**

```bash
git add Areas/Admin/Controllers/ComboController.cs
git commit -m "feat(combo): inject IImageUploadService into admin controller"
```

---

### Task 3: Add upload helper to Admin ComboController

**Files:**
- Modify: `Areas/Admin/Controllers/ComboController.cs`

**Interfaces:**
- Consumes: `_imageUploadService`, `ModelState`.
- Produces: private helper `TryUploadComboImageAsync` returning `Task<string?>`.

- [ ] **Step 1: Add helper method**

Add the following private method inside `ComboController`:

```csharp
private async Task<string?> TryUploadComboImageAsync(IFormFile? imageFile)
{
    if (imageFile == null || imageFile.Length == 0)
        return null;

    if (!_imageUploadService.IsValidImageFile(imageFile))
    {
        ModelState.AddModelError("ImageFile", "File không phải định dạng ảnh hợp lệ (.jpg, .jpeg, .png, .gif, .webp).");
        return null;
    }

    if (!_imageUploadService.IsValidFileSize(imageFile))
    {
        ModelState.AddModelError("ImageFile", "File vượt quá kích thước cho phép (5MB).");
        return null;
    }

    try
    {
        return await _imageUploadService.UploadImageAsync(imageFile, "combos");
    }
    catch (Exception ex)
    {
        ModelState.AddModelError("ImageFile", $"Không thể upload ảnh: {ex.Message}");
        return null;
    }
}
```

- [ ] **Step 2: Build**

Run:

```bash
dotnet build
```

Expected: build succeeds, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Areas/Admin/Controllers/ComboController.cs
git commit -m "feat(combo): add TryUploadComboImageAsync helper"
```

---

### Task 4: Handle image upload in Create action

**Files:**
- Modify: `Areas/Admin/Controllers/ComboController.cs:33-51`

**Interfaces:**
- Consumes: `TryUploadComboImageAsync` from Task 3.
- Produces: `Create` POST action sets `model.ImageUrl` from uploaded file.

- [ ] **Step 1: Modify Create POST action**

Replace the current `Create` POST action body with:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(ComboFormViewModel model)
{
    if (!ModelState.IsValid)
    {
        model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
        return View(model);
    }

    var uploadedUrl = await TryUploadComboImageAsync(model.ImageFile);
    if (uploadedUrl == null && model.ImageFile != null)
    {
        model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
        return View(model);
    }

    if (!string.IsNullOrEmpty(uploadedUrl))
        model.ImageUrl = uploadedUrl;

    var result = await _comboService.CreateAsync(model);
    if (!result.Success)
    {
        ModelState.AddModelError("", result.ErrorMessage ?? "Có lỗi xảy ra");
        model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
        return View(model);
    }

    TempData["Success"] = "Tạo combo món ăn thành công!";
    return RedirectToAction(nameof(Index));
}
```

- [ ] **Step 2: Build**

Run:

```bash
dotnet build
```

Expected: build succeeds, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Areas/Admin/Controllers/ComboController.cs
git commit -m "feat(combo): upload image in admin Create action"
```

---

### Task 5: Handle image upload and replace in Edit action

**Files:**
- Modify: `Areas/Admin/Controllers/ComboController.cs:66-84`

**Interfaces:**
- Consumes: `TryUploadComboImageAsync`, `_imageUploadService.DeleteImageAsync`.
- Produces: `Edit` POST action replaces old image when a new one is uploaded.

- [ ] **Step 1: Modify Edit POST action**

Replace the current `Edit` POST action body with:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(int id, ComboFormViewModel model)
{
    if (!ModelState.IsValid)
    {
        model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
        return View(model);
    }

    if (model.ImageFile != null)
    {
        var existing = await _comboService.GetForEditAsync(id);
        if (!string.IsNullOrEmpty(existing?.ImageUrl))
            await _imageUploadService.DeleteImageAsync(existing.ImageUrl);

        var uploadedUrl = await TryUploadComboImageAsync(model.ImageFile);
        if (uploadedUrl == null)
        {
            model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
            return View(model);
        }

        model.ImageUrl = uploadedUrl;
    }

    var result = await _comboService.UpdateAsync(id, model);
    if (!result.Success)
    {
        ModelState.AddModelError("", result.ErrorMessage ?? "Có lỗi xảy ra");
        model.Products = (await _comboService.GetProductOptionsAsync()).ToList();
        return View(model);
    }

    TempData["Success"] = "Cập nhật combo món ăn thành công!";
    return RedirectToAction(nameof(Index));
}
```

- [ ] **Step 2: Build**

Run:

```bash
dotnet build
```

Expected: build succeeds, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Areas/Admin/Controllers/ComboController.cs
git commit -m "feat(combo): upload and replace image in admin Edit action"
```

---

### Task 6: Update combo form view to use file input

**Files:**
- Modify: `Areas/Admin/Views/Combo/_ComboForm.cshtml`

**Interfaces:**
- Consumes: `ComboFormViewModel.ImageFile`, `ComboFormViewModel.ImageUrl`.
- Produces: form renders a file input and current image preview.

- [ ] **Step 1: Add multipart enctype to form**

Change line 3 from:

```html
<form asp-action="@(Model.Id == 0 ? "Create" : "Edit")" method="post">
```

To:

```html
<form asp-action="@(Model.Id == 0 ? "Create" : "Edit")" method="post" enctype="multipart/form-data">
```

- [ ] **Step 2: Replace ImageUrl input with ImageFile input**

Change the ImageUrl input block:

```html
<div class="col-md-6 mb-3">
    <label asp-for="ImageUrl" class="form-label">Hình ảnh (URL)</label>
    <input asp-for="ImageUrl" class="form-control" placeholder="URL hình ảnh combo" />
</div>
```

To:

```html
<div class="col-md-6 mb-3">
    <label asp-for="ImageFile" class="form-label">Hình ảnh</label>
    <input asp-for="ImageFile" class="form-control" type="file" accept="image/*" />
    <span asp-validation-for="ImageFile" class="text-danger"></span>
    @if (!string.IsNullOrEmpty(Model.ImageUrl))
    {
        <div class="mt-2">
            <img src="@Model.ImageUrl" alt="Ảnh combo hiện tại" style="max-height: 120px;" class="img-thumbnail" />
            <small class="text-muted d-block">Upload ảnh mới để thay thế</small>
        </div>
    }
</div>
```

- [ ] **Step 3: Build**

Run:

```bash
dotnet build
```

Expected: build succeeds, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Areas/Admin/Views/Combo/_ComboForm.cshtml
git commit -m "feat(combo): replace ImageUrl input with file upload input"
```

---

### Task 7: Verify end-to-end

**Files:**
- Test manually via browser/Playwright.

**Interfaces:**
- Consumes: all previous tasks.

- [ ] **Step 1: Start the application**

```bash
dotnet run --urls "http://localhost:5270"
```

Wait until the application is listening.

- [ ] **Step 2: Log in as superadmin**

Navigate to `http://localhost:5270/Account/Login` and log in with:
- Email: `superadmin@fruitables.com`
- Password: `Admin@123`

- [ ] **Step 3: Create combo with uploaded image**

1. Go to `http://localhost:5270/Admin/Combo/Create`.
2. Fill combo name, e.g. `Combo Upload Test`.
3. Click **Thêm món** and select a product.
4. Choose an image file via the new file input.
5. Click **Lưu combo**.
6. Verify redirect to `/Admin/Combo` and the combo appears with the uploaded image thumbnail.
7. Verify the file exists at `wwwroot/uploads/combos/`.

- [ ] **Step 4: Edit combo and replace image**

1. Click **Sửa** on the newly created combo.
2. Verify the current image preview is shown.
3. Choose a different image file.
4. Save.
5. Verify the old file is removed from `wwwroot/uploads/combos/` and the new file exists.

- [ ] **Step 5: Verify storefront**

1. Go to `http://localhost:5270/Shop`.
2. Verify the combo card displays the uploaded image.

- [ ] **Step 6: Test invalid file**

1. Try creating a combo with a non-image file (e.g. `.txt`).
2. Verify the form returns with an error message and the combo is not created.

- [ ] **Step 7: Commit verification result**

If all manual tests pass, commit any remaining changes (if none, this step is a no-op):

```bash
git status
```

- [ ] **Step 8: Push to GitHub**

```bash
git push origin master
```

---

## Self-Review

**Spec coverage:**
- Replace URL input with upload: Task 6.
- Use existing `IImageUploadService`: Tasks 2-5.
- Store in `wwwroot/uploads/combos/`: Task 3 helper calls `UploadImageAsync(file, "combos")`.
- Validation formats/size: Task 3 helper.
- Delete old image on edit: Task 5.
- Keep `ImageUrl` and DB unchanged: Tasks 1, 4, 5.
- Storefront unchanged: no tasks touch storefront.

**Placeholder scan:** No TBD/TODO. Each step has concrete code/commands.

**Type consistency:** `ImageFile` is `IFormFile?` throughout. `TryUploadComboImageAsync` returns `Task<string?>`.
