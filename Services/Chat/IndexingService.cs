using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Fruitables.Constants;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fruitables.Services.Chat;

// ============================================================
// ĐƯA DỮ LIỆU VÀO "SỔ TRI THỨC" CỦA BOT
//
// Việc làm:
// - Lấy FAQ / sản phẩm / cài đặt công khai
// - Catalog insights: top bán chạy + nổi bật (template server, an toàn injection)
// - Cắt thành đoạn nhỏ
// - Mã hóa thành dãy số (embedding)
// - Lưu vào bảng KnowledgeChunks
//
// Nếu nội dung không đổi (hash giống) → không mã hóa lại (tiết kiệm thời gian).
// ============================================================
public sealed class IndexingService : IIndexingService
{
    // SourceId cố định cho KnowledgeSourceType.Catalog
    public const string CatalogBestsellersSourceId = "bestsellers";
    public const string CatalogFeaturedSourceId = "featured";

    private const int BestsellerTopN = 5;
    private const int BestsellerLookbackDays = 30;
    private const int MaxProductNameChars = 80;
    private const int MaxShortDescriptionChars = 200;
    private const int MaxDescriptionChars = 400;

    private readonly ApplicationDbContext _db;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly ILogger<IndexingService> _logger;
    private readonly ChatOptions _options;

    public IndexingService(
        ApplicationDbContext db,
        IEmbeddingClient embeddingClient,
        IOptions<ChatOptions> options,
        ILogger<IndexingService> logger)
    {
        _db = db;
        _embeddingClient = embeddingClient;
        _options = options?.Value ?? new ChatOptions();
        _logger = logger;
    }

    // Học 1 bài FAQ
    public async Task IndexFaqAsync(int faqId, CancellationToken ct = default)
    {
        var sourceId = faqId.ToString();
        var faq = await _db.Faqs.AsNoTracking().FirstOrDefaultAsync(f => f.Id == faqId, ct);

        // Không có / đã tắt → gỡ khỏi sổ tri thức
        if (faq is null || !faq.IsActive)
        {
            await DeactivateSourceAsync(KnowledgeSourceType.Faq, sourceId, ct);
            return;
        }

        // Thêm gợi ý từ khóa theo category (ship/sepay/…) để query ngắn của khách match tốt hơn
        var hints = RetrievalText.CategorySearchHints(faq.Category);
        var text = string.IsNullOrWhiteSpace(hints)
            ? $"{faq.Title}\n\n{faq.Body}"
            : $"{faq.Title}\n\n{faq.Body}\n\nTừ khóa: {hints}";
        var chunks = TextChunker.Chunk(text);
        await UpsertChunksAsync(KnowledgeSourceType.Faq, sourceId, faq.Title, chunks, ct);

        _logger.LogDebug("Indexed FAQ {FaqId} into {ChunkCount} chunk(s)", faqId, chunks.Count);
    }

    // Học 1 sản phẩm (tên, mô tả, danh mục...)
    public async Task IndexProductAsync(int productId, CancellationToken ct = default)
    {
        var sourceId = productId.ToString();
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Tags)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        // Xóa mềm / ẩn / không tồn tại → gỡ khỏi sổ
        if (product is null || product.IsDeleted || !product.IsActive)
        {
            await DeactivateSourceAsync(KnowledgeSourceType.Product, sourceId, ct);
            return;
        }

        var safeName = SanitizeCatalogText(product.Name, MaxProductNameChars);
        var text = BuildProductText(product);
        var chunks = TextChunker.Chunk(text);
        await UpsertChunksAsync(KnowledgeSourceType.Product, sourceId, safeName, chunks, ct);

        _logger.LogDebug("Indexed product {ProductId} into {ChunkCount} chunk(s)", productId, chunks.Count);
    }

    // Top bán chạy (từ OrderItems) + SP nổi bật — chỉ tên + số liệu, không dán full mô tả
    public async Task IndexCatalogInsightsAsync(CancellationToken ct = default)
    {
        var bestsellerText = await BuildBestsellersChunkAsync(ct);
        if (string.IsNullOrWhiteSpace(bestsellerText))
            await DeactivateSourceAsync(KnowledgeSourceType.Catalog, CatalogBestsellersSourceId, ct);
        else
            await UpsertChunksAsync(
                KnowledgeSourceType.Catalog,
                CatalogBestsellersSourceId,
                "Sản phẩm bán chạy nhất",
                new[] { bestsellerText },
                ct);

        var featuredText = await BuildFeaturedChunkAsync(ct);
        if (string.IsNullOrWhiteSpace(featuredText))
            await DeactivateSourceAsync(KnowledgeSourceType.Catalog, CatalogFeaturedSourceId, ct);
        else
            await UpsertChunksAsync(
                KnowledgeSourceType.Catalog,
                CatalogFeaturedSourceId,
                "Sản phẩm nổi bật",
                new[] { featuredText },
                ct);

        _logger.LogInformation(
            "Indexed catalog insights: bestsellers={HasBest}, featured={HasFeatured}",
            !string.IsNullOrWhiteSpace(bestsellerText),
            !string.IsNullOrWhiteSpace(featuredText));
    }

    // Học các cài đặt công khai (hotline, phí ship...)
    public async Task IndexAllowlistedSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _db.Settings.AsNoTracking().ToListAsync(ct);
        var byKey = settings.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        var indexedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in ChatSettingAllowlist.Keys)
        {
            byKey.TryGetValue(key, out var value);
            // Trống → không cho bot học key này
            if (string.IsNullOrWhiteSpace(value))
            {
                await DeactivateSourceAsync(KnowledgeSourceType.Setting, key, ct);
                continue;
            }

            // Nhãn tiếng Việt + từ khóa để query khách match (không chỉ key kỹ thuật)
            var (title, content) = FormatSettingChunk(key, value!);
            await UpsertChunksAsync(
                KnowledgeSourceType.Setting,
                key,
                title,
                new[] { content },
                ct);
            indexedKeys.Add(key);
        }

        // Tắt các mẩu setting lạ / không còn trong allowlist
        var activeSettingChunks = await _db.KnowledgeChunks
            .Where(c => c.SourceType == KnowledgeSourceType.Setting && c.IsActive)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var deactivated = 0;
        foreach (var chunk in activeSettingChunks)
        {
            if (!indexedKeys.Contains(chunk.SourceId))
            {
                chunk.IsActive = false;
                chunk.UpdatedAt = now;
                deactivated++;
            }
        }

        if (deactivated > 0)
            await _db.SaveChangesAsync(ct);

        _logger.LogDebug(
            "Indexed {IndexedCount} allowlisted setting(s); deactivated {DeactivatedCount} stale setting chunk(s)",
            indexedKeys.Count,
            deactivated);
    }

    // Nút Admin "Đồng bộ knowledge": học lại tất cả
    public async Task ReindexAllAsync(CancellationToken ct = default)
    {
        var faqIds = await _db.Faqs.AsNoTracking().Select(f => f.Id).ToListAsync(ct);
        foreach (var id in faqIds)
            await IndexFaqAsync(id, ct);

        var productIds = await _db.Products.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
        foreach (var id in productIds)
            await IndexProductAsync(id, ct);

        await IndexAllowlistedSettingsAsync(ct);
        await IndexCatalogInsightsAsync(ct);

        _logger.LogInformation(
            "ReindexAll complete: {FaqCount} FAQ(s), {ProductCount} product(s), settings, catalog",
            faqIds.Count,
            productIds.Count);
    }

    // Tắt hết mẩu tri thức của 1 nguồn (FAQ id X / SP id Y...)
    private async Task DeactivateSourceAsync(
        KnowledgeSourceType sourceType,
        string sourceId,
        CancellationToken ct)
    {
        var chunks = await _db.KnowledgeChunks
            .Where(c => c.SourceType == sourceType && c.SourceId == sourceId && c.IsActive)
            .ToListAsync(ct);

        if (chunks.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var chunk in chunks)
        {
            chunk.IsActive = false;
            chunk.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    // Thêm / cập nhật các đoạn của 1 nguồn; bỏ đoạn cũ không còn khớp
    private async Task UpsertChunksAsync(
        KnowledgeSourceType sourceType,
        string sourceId,
        string? title,
        IReadOnlyList<string> chunkTexts,
        CancellationToken ct)
    {
        // Các dòng cũ của nguồn này
        var existing = await _db.KnowledgeChunks
            .Where(c => c.SourceType == sourceType && c.SourceId == sourceId)
            .ToListAsync(ct);

        var newHashes = new HashSet<string>(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        // Tránh gán 1 dòng DB cho 2 đoạn mới
        var reusedIds = new HashSet<long>();

        foreach (var text in chunkTexts)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;

            // Hash gắn AlgorithmId → đổi thuật toán embed (lh-v2…) sẽ reindex lại vector
            var hash = ComputeContentHash(RetrievalText.AlgorithmId + "\0" + text);
            // Cùng nội dung trong 1 nguồn → chỉ giữ 1
            if (!newHashes.Add(hash))
                continue;

            // Đã có dòng cùng hash? → tái sử dụng, không gọi embed lại
            var match = existing.FirstOrDefault(c =>
                    c.ContentHash == hash && c.IsActive && !reusedIds.Contains(c.Id))
                ?? existing.FirstOrDefault(c =>
                    c.ContentHash == hash && !reusedIds.Contains(c.Id));

            if (match is not null)
            {
                reusedIds.Add(match.Id);
                match.IsActive = true;
                match.Title = title;
                match.Content = text;
                match.UpdatedAt = now;
                continue;
            }

            // Nội dung mới → mã hóa + thêm dòng
            var embedding = await _embeddingClient.EmbedAsync(text, ct);
            _db.KnowledgeChunks.Add(new KnowledgeChunk
            {
                SourceType = sourceType,
                SourceId = sourceId,
                Title = title,
                Content = text,
                EmbeddingJson = EmbeddingSerializer.ToJson(embedding),
                ContentHash = hash,
                IsActive = true,
                UpdatedAt = now
            });
        }

        // Đoạn cũ không còn trong bản mới → tắt
        foreach (var chunk in existing)
        {
            if (!newHashes.Contains(chunk.ContentHash) && chunk.IsActive)
            {
                chunk.IsActive = false;
                chunk.UpdatedAt = now;
            }
            else if (newHashes.Contains(chunk.ContentHash)
                     && chunk.IsActive
                     && !reusedIds.Contains(chunk.Id))
            {
                // Bản sao trùng hash không được dùng
                chunk.IsActive = false;
                chunk.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // Đổi setting kỹ thuật → đoạn tri thức dễ tìm (RAG local hash / lexical)
    internal static (string Title, string Content) FormatSettingChunk(string key, string value)
    {
        var v = value.Trim();
        return key switch
        {
            SettingKeys.ShippingFeeZone1 => (
                "Phí ship nội thành zone 1",
                $"Phí vận chuyển nội thành (zone 1) là {v} đồng.\nTừ khóa: phí ship nội thành zone 1 shipping fee zone1 {v}"),
            SettingKeys.ShippingFeeZone2 => (
                "Phí ship tỉnh lân cận zone 2",
                $"Phí vận chuyển các tỉnh lân cận (zone 2) là {v} đồng.\nTừ khóa: phí ship zone 2 tỉnh lân cận shipping fee zone2 {v}"),
            SettingKeys.ShippingFeeZone3 => (
                "Phí ship tỉnh xa zone 3",
                $"Phí vận chuyển các tỉnh xa (zone 3) là {v} đồng.\nTừ khóa: phí ship zone 3 tỉnh xa shipping fee zone3 {v}"),
            SettingKeys.FreeShippingThreshold => (
                "Ngưỡng miễn phí ship",
                $"Đơn hàng từ {v} đồng trở lên được miễn phí vận chuyển.\nTừ khóa: miễn phí ship free shipping threshold ngưỡng freeship {v}"),
            SettingKeys.ContactWorkingHours => (
                "Giờ làm việc cửa hàng",
                $"Giờ làm việc / mở cửa: {v}.\nTừ khóa: giờ mở cửa giờ làm việc working hours liên hệ"),
            SettingKeys.ContactPhone => (
                "Số điện thoại liên hệ",
                $"Hotline / điện thoại liên hệ: {v}.\nTừ khóa: hotline sđt phone liên hệ"),
            SettingKeys.ContactEmail => (
                "Email liên hệ",
                $"Email liên hệ: {v}.\nTừ khóa: email liên hệ"),
            SettingKeys.ContactAddress => (
                "Địa chỉ cửa hàng",
                $"Địa chỉ cửa hàng: {v}.\nTừ khóa: địa chỉ cửa hàng contact address"),
            SettingKeys.SiteName => (
                "Tên cửa hàng",
                $"Tên cửa hàng: {v}."),
            _ => (key, $"{key}: {v}")
        };
    }

    // Gom chữ quan trọng của sản phẩm — đã sanitize + cắt ngắn (giảm document injection)
    public static string BuildProductText(Product product)
    {
        var parts = new List<string>();
        var unit = SanitizeCatalogText(product.Unit, 20);

        parts.Add($"Tên sản phẩm: {SanitizeCatalogText(product.Name, MaxProductNameChars)}");

        if (product.Category is not null && !string.IsNullOrWhiteSpace(product.Category.Name))
            parts.Add($"Danh mục: {SanitizeCatalogText(product.Category.Name, MaxProductNameChars)}");

        parts.Add($"Giá: {product.Price.ToString("#,##0", CultureInfo.InvariantCulture)}đ / {unit}");

        if (product.SalePrice.HasValue && product.SalePrice.Value > 0 && product.SalePrice.Value < product.Price)
            parts.Add($"Giá khuyến mãi: {product.SalePrice.Value.ToString("#,##0", CultureInfo.InvariantCulture)}đ / {unit}");

        if (product.StockQuantity > 0)
            parts.Add($"Tồn kho: {product.StockQuantity} {unit} (còn hàng)");
        else
            parts.Add($"Tồn kho: 0 {unit}");

        if (product.MinOrderQuantity > 1)
            parts.Add($"Mua tối thiểu: {product.MinOrderQuantity} {unit}");

        if (!string.IsNullOrWhiteSpace(product.CountryOrigin))
            parts.Add($"Xuất xứ: {SanitizeCatalogText(product.CountryOrigin, 40)}");

        if (!string.IsNullOrWhiteSpace(product.Quality))
            parts.Add($"Chất lượng / quy cách: {SanitizeCatalogText(product.Quality, 50)}");

        if (product.Weight.HasValue && product.Weight.Value > 0)
            parts.Add($"Khối lượng: {product.Weight.Value.ToString("#,##0.##", CultureInfo.InvariantCulture)} {unit}");

        if (!string.IsNullOrWhiteSpace(product.ShortDescription))
            parts.Add(SanitizeCatalogText(product.ShortDescription, MaxShortDescriptionChars));

        if (!string.IsNullOrWhiteSpace(product.Description))
            parts.Add(SanitizeCatalogText(product.Description, MaxDescriptionChars));

        if (product.Tags is not null && product.Tags.Count > 0)
        {
            var tags = string.Join(", ", product.Tags.Select(t => SanitizeCatalogText(t.Name, 30)));
            parts.Add($"Tag: {tags}");
        }

        var activeVariants = product.Variants?
            .Where(v => v.IsActive)
            .OrderBy(v => v.Name)
            .ToList();
        if (activeVariants is not null && activeVariants.Count > 0)
        {
            parts.Add("Biến thể:");
            foreach (var v in activeVariants)
            {
                var line = $"- {SanitizeCatalogText(v.Name, 60)}: {v.Price.ToString("#,##0", CultureInfo.InvariantCulture)}đ";
                if (v.SalePrice.HasValue && v.SalePrice.Value > 0 && v.SalePrice.Value < v.Price)
                    line += $" (khuyến mãi {v.SalePrice.Value.ToString("#,##0", CultureInfo.InvariantCulture)}đ)";
                line += $", tồn kho {v.StockQuantity}";
                parts.Add(line);
            }
        }

        if (product.IsFeatured)
            parts.Add("Sản phẩm nổi bật featured.\nTừ khóa: nổi bật featured gợi ý");

        parts.Add("Từ khóa: sản phẩm product giá price tiền cost tồn kho stock còn hàng hết hàng khuyến mãi sale giảm giá variant biến thể");

        return string.Join("\n\n", parts);
    }

    // Template top bán chạy — chỉ tên đã sanitize + số lượng (server aggregate)
    internal async Task<string?> BuildBestsellersChunkAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-BestsellerLookbackDays);
        var excluded = new[] { OrderStatus.Cancelled, OrderStatus.Returned };

        // Group theo ProductId; lấy tên từ Product đang active nếu còn, không tin ProductName snapshot dài
        var rows = await (
            from oi in _db.OrderItems.AsNoTracking()
            join o in _db.Orders.AsNoTracking() on oi.OrderId equals o.Id
            join p in _db.Products.AsNoTracking() on oi.ProductId equals p.Id
            where !excluded.Contains(o.Status)
                  && o.CreatedAt >= since
                  && p.IsActive
                  && !p.IsDeleted
            group oi by new { oi.ProductId, p.Name } into g
            select new
            {
                g.Key.ProductId,
                g.Key.Name,
                Qty = g.Sum(x => x.Quantity)
            })
            .OrderByDescending(x => x.Qty)
            .Take(BestsellerTopN)
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            // Không có đơn → fallback gợi ý featured (vẫn template sạch)
            var featuredNames = await _db.Products.AsNoTracking()
                .Where(p => p.IsActive && !p.IsDeleted && p.IsFeatured)
                .OrderBy(p => p.Name)
                .Select(p => p.Name)
                .Take(BestsellerTopN)
                .ToListAsync(ct);

            if (featuredNames.Count == 0)
                return null;

            var fb = new StringBuilder();
            fb.AppendLine("Chưa có đủ dữ liệu đơn hàng 30 ngày để xếp hạng bán chạy chính xác.");
            fb.AppendLine("Gợi ý sản phẩm nổi bật hiện có:");
            for (var i = 0; i < featuredNames.Count; i++)
            {
                fb.Append(i + 1);
                fb.Append(". ");
                fb.AppendLine(SanitizeCatalogText(featuredNames[i], MaxProductNameChars));
            }

            fb.AppendLine("Từ khóa: sản phẩm bán chạy nhất best seller top hot nổi bật featured");
            return fb.ToString().Trim();
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Sản phẩm bán chạy nhất tại Fruitables ({BestsellerLookbackDays} ngày gần đây, theo số lượng đã bán):");
        for (var i = 0; i < rows.Count; i++)
        {
            var name = SanitizeCatalogText(rows[i].Name, MaxProductNameChars);
            sb.Append(i + 1);
            sb.Append(". ");
            sb.Append(name);
            sb.Append(" — ");
            sb.Append(rows[i].Qty);
            sb.AppendLine(" đơn vị đã bán");
        }

        sb.AppendLine("Từ khóa: sản phẩm bán chạy nhất best seller top hot ranking");
        return sb.ToString().Trim();
    }

    internal async Task<string?> BuildFeaturedChunkAsync(CancellationToken ct)
    {
        var names = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted && p.IsFeatured)
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .Take(10)
            .ToListAsync(ct);

        if (names.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("Sản phẩm nổi bật (featured) đang được giới thiệu tại Fruitables:");
        for (var i = 0; i < names.Count; i++)
        {
            sb.Append(i + 1);
            sb.Append(". ");
            sb.AppendLine(SanitizeCatalogText(names[i], MaxProductNameChars));
        }

        sb.AppendLine("Từ khóa: sản phẩm nổi bật featured gợi ý bán chạy");
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Làm sạch text đưa vào CONTEXT: bỏ control char, gộp khoảng trắng, cắt độ dài.
    /// Giảm rủi ro document/prompt injection từ tên/mô tả sản phẩm.
    /// </summary>
    public static string SanitizeCatalogText(string? text, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "(không tên)";

        var sb = new StringBuilder(text.Length);
        var prevSpace = false;
        foreach (var ch in text.Trim())
        {
            // Bỏ control (kể cả \0) — không để lọt vào CONTEXT/prompt
            if (ch < 32 || char.IsControl(ch))
            {
                if (ch is '\r' or '\n' or '\t')
                {
                    if (!prevSpace)
                    {
                        sb.Append(' ');
                        prevSpace = true;
                    }
                }
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace)
                {
                    sb.Append(' ');
                    prevSpace = true;
                }
                continue;
            }

            prevSpace = false;
            sb.Append(ch);
        }

        var s = sb.ToString().Trim();
        if (s.Length == 0)
            return "(không tên)";

        if (maxLen > 0 && s.Length > maxLen)
            s = s.Substring(0, maxLen).TrimEnd() + "…";

        return s;
    }

    // "Dấu vân tay" SHA256 của đoạn chữ (hex chữ thường)
    internal static string ComputeContentHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
