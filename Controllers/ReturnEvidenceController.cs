using System.Security.Claims;
using Fruitables.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fruitables.Controllers;

[Authorize]
public class ReturnEvidenceController : Controller
{
    private readonly IReturnEvidenceService _evidence;
    private readonly IRbacService _rbac;
    public ReturnEvidenceController(IReturnEvidenceService evidence, IRbacService rbac) { _evidence = evidence; _rbac = rbac; }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        if (isAdmin && !await _rbac.HasPermissionAsync(userId, "returns.view")) return Forbid();
        var result = await _evidence.OpenReadAsync(id, userId, isAdmin);
        if (result == null) return NotFound();
        Response.Headers.CacheControl = "private, no-store, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value.Content, result.Value.Evidence.MimeType, result.Value.Evidence.OriginalFileName);
    }
}
