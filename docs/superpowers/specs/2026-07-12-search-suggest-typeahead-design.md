# Search Suggest Typeahead — Design Spec

**Date:** 2026-07-12  
**Project:** Fruitables (ASP.NET Core 8 MVC e-commerce)  
**Status:** Approved for implementation planning  

## 1. Problem & goals

Storefront search today is submit-only:

- Navbar search modal, Home hero fields, and Shop search all POST/GET to `/Shop?search=…`
- Backend matches with `Name.Contains` / `Description.Contains` only
- No live suggestions while typing, so customers must complete a full navigation cycle to discover whether a product exists

**Goal (Phase 1):** A fast **typeahead suggest** system that improves discovery and reduces empty/frustrated searches.

### Success criteria

1. Customer typing in **any storefront search box** (navbar modal, Home, Shop) sees a dropdown of suggestions after the minimum query length.
2. Suggestions include three groups when matches exist: **products**, **categories**, **hot keywords**.
3. Clicking a product opens product detail; category opens Shop filtered by category; keyword opens Shop search; a **“Xem tất cả kết quả”** action (and default Enter without a selected row) goes to full Shop search for the current query.
4. Matching uses simple prefix/contains after light Vietnamese accent-insensitive normalization — not fuzzy typo or semantic/AI search.
5. Suggestions are sourced from **catalog + seeded hot keywords** only (no personalization).
6. API is public, rate-limited, and safe for XSS (escaped client rendering; server-built URLs).
7. Without JavaScript, existing form search continues to work (progressive enhancement).

## 2. Non-goals (Phase 1)

- Personalized history (“vừa tìm”, “vừa xem”)
- Typo-tolerant / fuzzy matching beyond normalization
- Semantic / embedding search
- External search engines (Meilisearch, Elasticsearch, Azure AI Search)
- SQL Server Full-Text Search setup
- Admin CRUD UI for hot keywords (seed + table is enough)
- Replacing or fully reworking Shop listing search ranking
- Autocomplete for Admin product search

## 3. Decisions summary

| Topic | Decision |
|---|---|
| UX type | Typeahead while typing |
| Suggestion kinds | Products + categories + hot keywords |
| Surfaces | All storefront search inputs |
| Navigation | Product → detail; category → Shop by id; keyword → Shop search; view-all / default submit → Shop search |
| Data sources | Active catalog + `SearchHotKeywords` seed |
| Match quality | Prefix preferred over contains; normalize trim/lower/strip Vietnamese accents |
| Architecture | In-process API + EF/SQL; shared JS module |
| Personalization | Deferred (P2) |

## 4. Architecture

```
[Navbar modal / Home / Shop inputs]
        │  search-suggest.js (debounce, keyboard, a11y)
        ▼
GET /api/search/suggest?q=…
        ▼
SearchSuggestController   validate length, rate limit
        ▼
ISearchSuggestService
  ├── SearchTextNormalizer
  ├── Products (active)
  ├── Categories
  └── SearchHotKeywords (active)
        ▼
JSON { query, products[], categories[], keywords[], viewAllUrl }
```

### Component responsibilities

| Unit | Responsibility | Depends on |
|---|---|---|
| `SearchTextNormalizer` | Canonical string for matching | None |
| `ISearchSuggestService` | Query groups, rank, map DTOs + URLs | EF, options, normalizer |
| `SearchSuggestController` | HTTP surface, validation, rate limit | Service |
| `SearchHotKeyword` entity + seed | Curated keyword suggestions | EF migration |
| `search-suggest.js` + CSS | Dropdown UX on all storefront fields | Suggest API |
| Existing Shop search | Full result page after submit / view-all | Unchanged core (optional later align) |

## 5. API

### `GET /api/search/suggest`

| Rule | Value |
|---|---|
| Auth | Public |
| Empty / whitespace `q` | HTTP 200 with empty groups + optional `viewAllUrl` for trimmed empty → treat as no suggestions |
| Min length before DB work | **2** characters after trim; shorter → empty groups |
| Max length | **50** (truncate or reject; prefer truncate for UX) |
| Rate limit | **60** requests / minute / IP (`IMemoryCache`, same idea as chat rate limit) |
| Caching | Optional short response cache later; not required for P1 |

### Response shape

```json
{
  "query": "tao",
  "products": [
    {
      "id": 12,
      "name": "Táo Fuji",
      "slug": "tao-fuji",
      "price": 125000,
      "salePrice": 99000,
      "imageUrl": "/uploads/...",
      "url": "/Product/Details/12"
    }
  ],
  "categories": [
    {
      "id": 3,
      "name": "Trái cây",
      "slug": "trai-cay",
      "url": "/Shop?categoryId=3"
    }
  ],
  "keywords": [
    {
      "text": "táo fuji",
      "url": "/Shop?search=t%C3%A1o%20fuji"
    }
  ],
  "viewAllUrl": "/Shop?search=tao"
}
```

Notes:

- `viewAllUrl` uses the **raw trimmed query** (URL-encoded), not only the normalized form, so Shop receives a human query string.
- Product `url` must use the **existing** product detail route of the app (confirm at implement time; do not invent a second detail URL scheme).
- Image URL: primary product image if available; client shows placeholder on error.

## 6. Data model

### Products / categories (existing)

| Source | Include when | Match on (P1) |
|---|---|---|
| Product | `IsActive` and not soft-deleted (if applicable) | `Name` only |
| Category | Existing storefront-visible rules (active / used as today) | `Name` |

Do **not** require new columns on Product/Category in P1.

### `SearchHotKeywords` (new)

| Column | Notes |
|---|---|
| Id | PK |
| Text | Display string |
| NormalizedText | Precomputed via normalizer for matching |
| SortOrder or Weight | Higher = prefer within keyword group |
| IsActive | Soft disable |
| CreatedAt | Audit |

**Seed:** 8–15 shop-relevant keywords (e.g. táo, cam, nho, rau củ, combo, giao hàng nhanh as marketing phrases only if they map to real shop search intent — prefer product/category-like phrases). No admin UI in P1.

## 7. Normalization & ranking

### Normalizer

1. Trim  
2. Lowercase  
3. Strip Vietnamese diacritics (`ả→a`, `đ→d`, …)  
4. Collapse internal whitespace  

### Matching strategy (P1)

1. EF/SQL **coarse filter** with simple `Contains` on original fields (or equivalent) to bound candidates.  
2. **In-memory** re-score with normalizer:  
   - Prefer **normalized prefix** (`StartsWith`) over **normalized contains**.  
   - Keywords: apply `Weight` / `SortOrder` after match class.  
   - Products: optional tie-break `IsFeatured` then name.  
3. Cap results: **5 products**, **3 categories**, **5 keywords** (configurable).

This avoids requiring SQL accent-insensitive FTS while staying correct for Vietnamese shop catalog scale.

**Out of scope P1:** persisted `NameNormalized` columns (P1.1/P2 if performance demands).

## 8. UI / client

### Surfaces

Attach suggest behavior to storefront search inputs:

- `Views/Shared/_SearchModal.cshtml`
- `Views/Home/Index.cshtml` search fields
- `Views/Shop/Index.cshtml` (`#shopSearch` and any equivalent)

Prefer a stable marker such as `data-search-suggest` in addition to `name="search"` so accidental non-storefront fields are not wired.

### Behavior

| Behavior | Spec |
|---|---|
| Debounce | 200–250ms after last keystroke |
| Min length | 2 before request |
| Dropdown | Anchored under the active input; high z-index (works inside fullscreen search modal) |
| Groups | Headings: Sản phẩm / Danh mục / Gợi ý — omit empty groups |
| Product row | Thumbnail (optional), name, price (show sale price when present) |
| Category / keyword row | Icon + text |
| Footer | “Xem tất cả kết quả cho ‘{q}’” → `viewAllUrl` |
| Keyboard | Arrow up/down, Enter activates selection or view-all if none, Escape closes |
| Outside click | Closes dropdown |
| Race | Ignore stale responses when `q` ≠ current input |
| Empty groups | Short empty state + keep view-all when query length ≥ min |
| Errors (network/5xx/429) | Fail soft: hide or leave previous safe state; form submit still works |
| a11y | listbox/option pattern, `aria-expanded`, `aria-activedescendant` |
| XSS | Escape all text nodes; do not inject raw HTML from API |

Progressive enhancement: without JS, GET form to Shop remains.

## 9. Configuration

```json
{
  "SearchSuggest": {
    "MinQueryLength": 2,
    "MaxQueryLength": 50,
    "MaxProducts": 5,
    "MaxCategories": 3,
    "MaxKeywords": 5,
    "RateLimitPerMinute": 60
  }
}
```

Debounce lives in JS (not server-enforced).

## 10. Error handling

| Condition | Behavior |
|---|---|
| Query too short | Empty groups, no error |
| Rate limit | HTTP 429; client soft-fail |
| Server exception | HTTP 503 or 500 with generic message; client soft-fail |
| Missing product image | Client placeholder |
| Invalid session / auth | N/A (public endpoint) |

## 11. Testing

| Layer | Coverage |
|---|---|
| Unit | Normalizer diacritics and whitespace |
| Service | Prefix ranks above contains; inactive product/keyword excluded; caps; min length short-circuit |
| API | 200 shape; short query empty; optional rate-limit |
| Manual | Navbar modal, Home, Shop; keyboard; view-all; no-JS form still works |

Use EF InMemory or existing test DB patterns; no external services.

## 12. Security

- Public read-only endpoint; no PII in response beyond public catalog fields  
- Rate limit by IP  
- Max query length  
- Client-side escape of names/texts  
- URLs built server-side from known routes/ids or encoded search text  

## 13. Roadmap

| Phase | Scope |
|---|---|
| **P1** | This document |
| P1.1 | Admin hot-keyword CRUD; optional search query logging → weights |
| P2 | Recent searches / personalization; `NameNormalized` or FTS if scale requires; optional Shop listing normalize align |

## 14. Risks

| Risk | Mitigation |
|---|---|
| In-memory rank misses if coarse SQL filter too strict | Coarse filter uses broad `Contains` on original text; normalize only for scoring |
| Modal z-index / overflow clips dropdown | CSS/z-index tested in search modal |
| Duplicate JS init on multiple inputs | Single shared module; mark bound inputs |
| Stale async responses | Compare request `q` to current value |
| Hot keywords go stale | Seed review; P1.1 admin |

## 15. Implementation notes

- Follow Fruitables patterns: `Services/` + interfaces, `Controllers/Api/`, `Options/`, EF migration, tests under `Tests/`.  
- Prefer small modules (`SearchTextNormalizer`, `SearchSuggestService`) over bloating `ProductService`.  
- Confirm product detail URL helper/route at implement time.  
- Do not commit secrets; no new third-party packages required for P1.

---

*End of Phase 1 design spec.*
