using System.Text;

namespace Fruitables.Services.Chat;

// ============================================================
// Chuß║⌐n h├│a chß╗» cho t├¼m kiß║┐m RAG (embedding local + ─æiß╗âm tß╗½ kh├│a).
//
// - T├ích token chß╗»/sß╗æ
// - Mß╗ƒ rß╗Öng synonym tiß║┐ng Viß╗çt Γåö tiß║┐ng Anh th╞░ß╗¥ng gß║╖p ß╗ƒ CS (ship, sepayΓÇª)
// - Bigram ─æß╗â bß║»t cß╗Ñm "ph├¡ ship", "bß║úo quß║ún"
// ============================================================
public static class RetrievalText
{
    // Phi├¬n bß║ún thuß║¡t to├ín ΓÇö ─æß╗òi khi sß╗¡a synonym/tokenize ΓåÆ reindex tß║ío lß║íi embedding
    public const string AlgorithmId = "lh-v4";

    // synonym: token gß╗æc ΓåÆ c├íc token li├¬n quan (─æ├ú lower-case)
    private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ship"] = new[] { "vß║¡n", "chuyß╗ân", "giao", "h├áng", "shipping", "vanchuyen", "giaohang", "cod" },
        ["shipping"] = new[] { "vß║¡n", "chuyß╗ân", "giao", "h├áng", "ship", "vanchuyen" },
        ["vß║¡n"] = new[] { "ship", "shipping", "giao" },
        ["chuyß╗ân"] = new[] { "ship", "shipping", "giao" },
        ["giao"] = new[] { "ship", "shipping", "vß║¡n", "chuyß╗ân", "h├áng" },
        ["sepay"] = new[] { "thanh", "to├ín", "qr", "chuyß╗ân", "khoß║ún", "thanhtoan", "payment" },
        ["qr"] = new[] { "sepay", "thanh", "to├ín", "qu├⌐t" },
        ["thanh"] = new[] { "sepay", "payment", "to├ín" },
        ["to├ín"] = new[] { "sepay", "payment", "thanh" },
        ["payment"] = new[] { "thanh", "to├ín", "sepay", "qr" },
        ["bß║úo"] = new[] { "quß║ún", "t╞░╞íi", "tß╗º", "lß║ính" },
        ["quß║ún"] = new[] { "bß║úo", "t╞░╞íi", "tß╗º", "lß║ính" },
        ["bß║úoquß║ún"] = new[] { "bß║úo", "quß║ún", "t╞░╞íi", "rau", "cß╗º" },
        ["giß╗¥"] = new[] { "l├ám", "viß╗çc", "mß╗ƒ", "cß╗¡a", "hours", "li├¬n", "hß╗ç" },
        ["─æ╞ín"] = new[] { "h├áng", "order", "tracking", "theo", "d├╡i" },
        ["─æß╗òi"] = new[] { "trß║ú", "return", "ho├án" },
        ["trß║ú"] = new[] { "─æß╗òi", "return", "ho├án" },
        ["return"] = new[] { "─æß╗òi", "trß║ú", "ho├án" },
        ["ph├¡"] = new[] { "ship", "vß║¡n", "chuyß╗ân", "gi├í", "c╞░ß╗¢c" },
        // Sß║ún phß║⌐m / b├ín chß║íy (C├ích A catalog)
        ["b├ín"] = new[] { "chß║íy", "best", "seller", "top", "hot", "nß╗òi" },
        ["chß║íy"] = new[] { "b├ín", "best", "seller", "top" },
        ["best"] = new[] { "b├ín", "chß║íy", "seller", "top", "nß╗òi" },
        ["seller"] = new[] { "b├ín", "chß║íy", "best", "top" },
        ["nß╗òi"] = new[] { "bß║¡t", "featured", "b├ín", "chß║íy", "gß╗úi" },
        ["bß║¡t"] = new[] { "nß╗òi", "featured" },
        ["featured"] = new[] { "nß╗òi", "bß║¡t", "b├ín", "chß║íy" },
        ["sß║ún"] = new[] { "phß║⌐m", "product" },
        ["phß║⌐m"] = new[] { "sß║ún", "product" },
        // Gi├í / tiß╗ün
        ["gi├í"] = new[] { "price", "tiß╗ün", "cost", "bao", "nhi├¬u" },
        ["tiß╗ün"] = new[] { "gi├í", "price", "cost" },
        ["price"] = new[] { "gi├í", "tiß╗ün", "cost" },
        ["cost"] = new[] { "gi├í", "tiß╗ün", "price" },
        ["bao"] = new[] { "nhi├¬u", "gi├í", "tiß╗ün" },
        ["nhi├¬u"] = new[] { "bao", "gi├í", "tiß╗ün" },
        // Tß╗ôn kho / c├▓n h├áng
        ["tß╗ôn"] = new[] { "kho", "stock", "c├▓n", "h├áng" },
        ["kho"] = new[] { "tß╗ôn", "stock", "c├▓n", "h├áng" },
        ["stock"] = new[] { "tß╗ôn", "kho", "c├▓n", "h├áng" },
        ["c├▓n"] = new[] { "tß╗ôn", "kho", "stock", "h├áng" },
        ["h├áng"] = new[] { "tß╗ôn", "kho", "stock", "c├▓n", "sß║ún", "phß║⌐m" },
        ["hß║┐t"] = new[] { "tß╗ôn", "kho", "stock", "c├▓n", "h├áng" },
        // Khuyß║┐n m├úi / giß║úm gi├í
        ["khuyß║┐n"] = new[] { "m├úi", "sale", "giß║úm", "gi├í" },
        ["m├úi"] = new[] { "khuyß║┐n", "sale", "giß║úm", "gi├í" },
        ["sale"] = new[] { "khuyß║┐n", "m├úi", "giß║úm", "gi├í" },
        ["giß║úm"] = new[] { "khuyß║┐n", "m├úi", "sale", "gi├í" },
    };

    // Gß╗úi ├╜ tß╗½ kh├│a theo category FAQ (index th├¬m v├áo chunk)
    private static readonly Dictionary<string, string> CategoryHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["shipping"] = "ph├¡ ship ph├¡ vß║¡n chuyß╗ân giao h├áng shipping free ship COD",
        ["payment"] = "thanh to├ín SePay QR chuyß╗ân khoß║ún payment qu├⌐t m├ú",
        ["product-care"] = "bß║úo quß║ún rau cß╗º t╞░╞íi tß╗º lß║ính product care",
        ["hours"] = "giß╗¥ l├ám viß╗çc mß╗ƒ cß╗¡a li├¬n hß╗ç hotline",
        ["order"] = "─æ╞ín h├áng lß╗ïch sß╗¡ theo d├╡i order tracking",
        ["return"] = "─æß╗òi trß║ú ho├án tiß╗ün return policy",
    };

    public static string? CategorySearchHints(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return null;
        return CategoryHints.TryGetValue(category.Trim(), out var hints) ? hints : null;
    }

    public static IReadOnlyList<string> Tokenize(string text)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        foreach (var ch in (text ?? string.Empty).ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
            else if (sb.Length > 0)
            {
                list.Add(sb.ToString());
                sb.Clear();
            }
        }

        if (sb.Length > 0)
            list.Add(sb.ToString());

        return list;
    }

    /// <summary>Token + synonym + bigram ΓÇö d├╣ng cho embedding local v├á lexical score.</summary>
    public static IReadOnlyList<string> ExpandTokens(string text)
    {
        var baseTokens = Tokenize(text);
        if (baseTokens.Count == 0)
            return Array.Empty<string>();

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in baseTokens)
        {
            set.Add(t);
            if (Synonyms.TryGetValue(t, out var syns))
            {
                foreach (var s in syns)
                    set.Add(s);
            }
        }

        // bigram tr├¬n token gß╗æc (kh├┤ng synonym) ─æß╗â giß╗» cß╗Ñm ngß║»n
        for (var i = 0; i < baseTokens.Count - 1; i++)
            set.Add(baseTokens[i] + "_" + baseTokens[i + 1]);

        return set.ToList();
    }

    /// <summary>
    /// Tß╗╖ lß╗ç token truy vß║Ñn (─æ├ú expand) xuß║Ñt hiß╗çn trong t├ái liß╗çu (─æ├ú expand).
    /// 1.0 = mß╗ìi ├╜ query ─æß╗üu c├│ trong doc.
    /// </summary>
    public static float QueryCoverage(string query, string document)
    {
        var q = ExpandTokens(query);
        if (q.Count == 0)
            return 0f;

        var d = new HashSet<string>(ExpandTokens(document), StringComparer.Ordinal);
        var hits = 0;
        foreach (var t in q)
        {
            if (d.Contains(t))
                hits++;
        }

        return (float)hits / q.Count;
    }
}
