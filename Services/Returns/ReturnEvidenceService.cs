using System.Security.Cryptography;
using Fruitables.Data;
using Fruitables.Models.Returns;
using Fruitables.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Returns;

public class ReturnEvidenceService : IReturnEvidenceService
{
    private static readonly Dictionary<string, (string Mime, long Max)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ("image/jpeg", 10 * 1024 * 1024), [".jpeg"] = ("image/jpeg", 10 * 1024 * 1024),
        [".png"] = ("image/png", 10 * 1024 * 1024), [".webp"] = ("image/webp", 10 * 1024 * 1024),
        [".mp4"] = ("video/mp4", 30 * 1024 * 1024)
    };
    private readonly ApplicationDbContext _db;
    private readonly string _root;
    private readonly TimeProvider _clock;

    public ReturnEvidenceService(ApplicationDbContext db, IWebHostEnvironment environment, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
        _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "ReturnEvidence"));
    }

    public async Task<(bool Success, string? Error, ReturnEvidence? Evidence)> UploadAsync(int returnRequestId, int? returnItemId, int userId, IFormFile file, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var request = await _db.ReturnRequests.SingleOrDefaultAsync(x => x.Id == returnRequestId, cancellationToken);
        if (request == null || (!isAdmin && request.UserId != userId)) return (false, "Không tìm thấy yêu cầu hoặc bạn không có quyền truy cập.", null);
        if (!isAdmin && request.Status is not (ReturnRequestStatus.Submitted or ReturnRequestStatus.AwaitingEvidence)) return (false, "Yêu cầu không cho phép bổ sung bằng chứng.", null);
        if (!isAdmin && request.Status == ReturnRequestStatus.AwaitingEvidence && request.EvidenceDueAtUtc < _clock.GetUtcNow().UtcDateTime) return (false, "Đã hết thời hạn bổ sung bằng chứng.", null);
        if (returnItemId.HasValue && !await _db.ReturnRequestItems.AnyAsync(x => x.Id == returnItemId && x.ReturnRequestId == returnRequestId, cancellationToken)) return (false, "Sản phẩm không thuộc yêu cầu.", null);
        var extension = Path.GetExtension(Path.GetFileName(file.FileName));
        if (!Allowed.TryGetValue(extension, out var rule) || !string.Equals(file.ContentType, rule.Mime, StringComparison.OrdinalIgnoreCase)) return (false, "Định dạng file không được hỗ trợ.", null);
        if (file.Length <= 0 || file.Length > rule.Max) return (false, "Dung lượng file không hợp lệ.", null);
        var current = await _db.ReturnEvidences.Where(x => x.ReturnRequestId == returnRequestId).Select(x => new { x.SizeBytes, x.MimeType }).ToListAsync(cancellationToken);
        if (current.Count >= 5 || current.Sum(x => x.SizeBytes) + file.Length > 40L * 1024 * 1024) return (false, "Đã vượt giới hạn file của yêu cầu.", null);
        if (rule.Mime == "video/mp4" && current.Any(x => x.MimeType == "video/mp4")) return (false, "Mỗi yêu cầu chỉ được tải một video.", null);

        await using var input = file.OpenReadStream();
        var header = new byte[Math.Min(16, (int)file.Length)];
        var read = await input.ReadAsync(header, cancellationToken);
        if (!MatchesSignature(rule.Mime, header.AsSpan(0, read))) return (false, "Nội dung file không khớp định dạng khai báo.", null);
        input.Position = 0;
        Directory.CreateDirectory(_root);
        var storageKey = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = Path.GetFullPath(Path.Combine(_root, storageKey));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return (false, "Đường dẫn lưu file không hợp lệ.", null);
        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true)) await input.CopyToAsync(output, cancellationToken);
        await using var checksumStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var checksum = Convert.ToHexString(await SHA256.HashDataAsync(checksumStream, cancellationToken)).ToLowerInvariant();
        var evidence = new ReturnEvidence { ReturnRequestId = returnRequestId, ReturnRequestItemId = returnItemId, UploadedByUserId = userId, OriginalFileName = SafeName(file.FileName), StorageKey = storageKey, MimeType = rule.Mime, SizeBytes = file.Length, Sha256Checksum = checksum, ScanStatus = EvidenceScanStatus.Pending, IsInternal = isAdmin, UploadedAtUtc = _clock.GetUtcNow().UtcDateTime };
        _db.ReturnEvidences.Add(evidence);
        _db.ReturnEvents.Add(new ReturnEvent { ReturnRequestId = returnRequestId, Type = ReturnEventType.EvidenceAdded, ActorUserId = userId, Note = "Đã bổ sung bằng chứng.", CreatedAtUtc = evidence.UploadedAtUtc });
        try { await _db.SaveChangesAsync(cancellationToken); return (true, null, evidence); }
        catch { File.Delete(path); throw; }
    }

    public async Task<(ReturnEvidence Evidence, Stream Content)?> OpenReadAsync(int evidenceId, int userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var evidence = await _db.ReturnEvidences.AsNoTracking().Include(x => x.ReturnRequest).SingleOrDefaultAsync(x => x.Id == evidenceId, cancellationToken);
        if (evidence == null || (!isAdmin && (evidence.ReturnRequest.UserId != userId || evidence.IsInternal))) return null;
        var path = Path.GetFullPath(Path.Combine(_root, evidence.StorageKey));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return null;
        return (evidence, new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true));
    }

    private static string SafeName(string name) => new(Path.GetFileName(name).Where(c => !char.IsControl(c)).Take(255).ToArray());
    private static bool MatchesSignature(string mime, ReadOnlySpan<byte> h) => mime switch
    {
        "image/jpeg" => h.Length >= 3 && h[0] == 0xff && h[1] == 0xd8 && h[2] == 0xff,
        "image/png" => h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/webp" => h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8),
        "video/mp4" => h.Length >= 12 && h[4..8].SequenceEqual("ftyp"u8),
        _ => false
    };
}
