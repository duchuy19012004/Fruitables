# Chat Intent Routing Design

**Goal:** Make the Fruitables chatbox helpful with natural-language questions while refusing sensitive requests safely and answering only from current store data or approved knowledge.

## Scope

This design changes how an incoming chat message is classified and routed. It does not grant the model access to admin accounts, credentials, internal configuration, or arbitrary customer data.

## Design

### 1. Sensitive-request guard

Before retrieval, inspect the user message for requests for credentials, admin accounts or permissions, API keys, passwords, connection strings, internal configuration, another customer’s data, or system prompts.

The guard returns a short fixed response and `Refused = true`. It does not search the database, call the LLM, or reveal whether the requested data exists.

Response:

> Mình không thể hỗ trợ thông tin tài khoản hoặc quyền quản trị. Mình có thể giúp về sản phẩm, đơn hàng, giao hàng và thanh toán.

### 2. Intent normalization

For messages that pass the sensitive guard, the local `cx/gpt-5.6-luna` endpoint receives a constrained classification prompt. It returns structured intent candidates and Vietnamese search phrases; it must not answer the customer’s question.

Supported routes:

- `product_search` — product availability, names, categories, combinations, price discovery.
- `order_support` — the signed-in user’s own order history and status only.
- `store_knowledge` — shipping, payments, policies, preservation, contact, and approved FAQ content.
- `unknown` — no reliable route.

The system always retains the original message and uses the generated phrases only as supplementary retrieval queries.

### 3. Product route

`product_search` queries active products directly by normalized name, slug, category, and generated search phrases. It never uses model memory to claim that a product, price, or stock level exists.

- Matches found: return at most three products with current name, price, availability status, and product URL.
- No matches: state that the store does not currently show a matching item and offer nearby categories or the product search page.

Example: “Sóp có bán iPhone không?” becomes phrases such as `iphone`, then checks the active catalog. It must not answer “có” without a database match.

### 4. Store-knowledge route

`store_knowledge` runs hybrid retrieval for the original message and normalized phrases. Their top results are deduplicated, ranked by the existing hybrid score, and accepted only when the best result meets `Chat:MinScore`.

Only the retrieved context is sent to the answer-generation prompt. This preserves the existing rule that the model cannot invent policy, delivery fee, price, stock, or account information.

### 5. Unknown and insufficient-data UX

When no route has a reliable result, return one concise sentence:

> Mình chưa tìm thấy thông tin phù hợp cho câu này.

The browser renders it as an action state, not a repeated contact paragraph:

- `Tìm sản phẩm` focuses the input and suggests product-search wording.
- `Phí giao hàng` sends the existing shipping chip.
- `Thanh toán` sends the existing payment chip.
- `Liên hệ CSKH` links to `/Contact`.

The backend response must not include a duplicate contact sentence; the UI owns the optional contact action.

## Data Flow

1. Receive message and apply existing length/rate limits.
2. Run sensitive-request guard.
3. Normalize intent and search phrases with the local LLM.
4. Route to active catalog, signed-in user order support, or hybrid store-knowledge retrieval.
5. Produce an answer only from route-approved current data.
6. Return the concise unknown state plus `Refused = true` when no trusted result exists.
7. Render unknown state with action buttons in the client.

## Failure Handling

- If intent normalization times out or fails, continue with existing hybrid retrieval using the original message; do not fail the whole chat request.
- If product/order data is unavailable, state that the relevant information is temporarily unavailable; do not claim an item or order status.
- If no trustworthy answer remains, use the concise unknown state and actions.

## Testing

- Sensitive requests return the fixed safe response without calling product, order, retrieval, or answer LLM services.
- Natural-language product questions resolve to live matching products only.
- Missing products never claim availability and return category/search actions.
- Natural-language policy phrasing reaches approved knowledge through normalized phrases.
- An intent-normalization outage falls back to original-query retrieval.
- The client renders a single concise unknown message and exactly one contact action.

## Constraints

- The local model is used only for classification and query normalization, not as a source of store facts.
- Product, price, stock, and order data must be read from the application database at request time.
- Order information is limited to the authenticated user’s own orders.
- No new external AI provider or API key is introduced.
