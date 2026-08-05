# SmallTalk Intent Design

## Goal

Handle common social messages without sending them through RAG, local embeddings,
or the MiMo API. Preserve existing business-intent routing for messages that also
contain a product, order, coupon, or shipping question.

## Design

Add `ChatIntentKind.SmallTalk` and classify five rule-based groups in
`IntentRouter`:

- greeting: `chào`, `xin chào`, `hello`, `hi`
- thanks/apology: `cảm ơn`, `cám ơn`, `xin lỗi`
- goodbye: `tạm biệt`, `hẹn gặp lại`
- acknowledgement: `ok`, `được`, `ừ`, `vâng`, `rồi`
- capability: questions such as `bạn có thể giúp gì?`

Business-intent keywords take precedence when a message combines social text with
a business request, e.g. `cảm ơn, phí ship bao nhiêu?` remains `ShippingQuote`.

`ChatService` handles `SmallTalk` with fixed Vietnamese replies in both normal
and streaming flows. It persists the message as usual but does not call RAG,
embedding, or `ILlmClient`.

## Testing

Add intent-routing tests for every group and a precedence test for a combined
social/business message. Add service tests proving `SmallTalk` returns the fixed
reply without invoking RAG.

## Scope

No changes to provider/model configuration, RAG thresholds, database schema, or
chat rate limits.
