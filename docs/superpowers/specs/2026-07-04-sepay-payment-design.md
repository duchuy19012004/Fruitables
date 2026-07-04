# SePay Payment Integration Design

## Goal

Integrate SePay bank-transfer payment for Fruitables orders so customers can scan a QR code after checkout and the site automatically marks the order as paid when SePay reports the matching bank transaction.

## Recommendation

Use a dedicated payment code on each order, not the public order number.

Format: `FTB` + 8 uppercase alphanumeric characters, for example `FTB7K3P9Q2`.

Reasoning: the order number is for people; the payment code is for SePay and bank reconciliation. Keeping them separate makes the webhook parser simple, avoids exposing sequential internal IDs, and lets the order number format change later without breaking payment matching.

## SePay Setup

In SePay dashboard:

1. Enable payment-code recognition.
2. Add a payment-code pattern with prefix `FTB`.
3. Configure suffix length as 8 characters.
4. Use alphanumeric suffix.
5. Create an incoming-money webhook for the production HTTPS endpoint.
6. Enable HMAC-SHA256 authentication.
7. Enable retry and alerts.

Relevant docs:

- https://developer.sepay.vn/vi/sepay-webhooks/cau-hinh-ma-thanh-toan
- https://developer.sepay.vn/vi/sepay-webhooks/tao-qr-va-form-thanh-toan
- https://developer.sepay.vn/vi/sepay-webhooks/tich-hop-webhook
- https://developer.sepay.vn/vi/sepay-webhooks/xac-thuc

## Application Changes

Add `PaymentCode` to `Order`.

- Nullable for old orders.
- Unique index when present.
- Generated only once, when creating a bank-transfer order.
- Stored with max length 16.

Add a small `SePayTransaction` log table.

- `SePayTransactionId` from webhook payload field `id`, unique.
- `OrderId`.
- `PaymentCode`.
- `TransferAmount`.
- `ReferenceCode`.
- `Status`, for example `Paid`, `Duplicate`, or `Ignored`.
- `Message`.
- `Payload`.
- `CreatedAt`.

This table is the idempotency guard. If SePay retries or an admin replays a webhook, the unique transaction id prevents double-processing.

## Checkout Flow

1. Customer chooses `BankTransfer`.
2. `OrderService.CreateOrderAsync` creates the order with `PaymentStatus.Pending`.
3. For bank transfer, it also generates `PaymentCode`.
4. Checkout redirects to the existing confirmation page.
5. Confirmation page shows:
   - Order total.
   - Payment code.
   - Bank account details from config.
   - VietQR image URL:
     `https://vietqr.app/img?acc={account}&bank={bank}&amount={total}&des={paymentCode}`

COD orders keep the current confirmation behavior.

## Webhook Flow

Endpoint: `POST /api/sepay/webhook`.

Processing:

1. Read raw body.
2. Verify HMAC using `X-SePay-Signature` and `X-SePay-Timestamp`.
3. Parse JSON payload.
4. If transaction id was already logged, return `{"success": true}`.
5. Require `transferType == "in"`.
6. Require `code` starts with `FTB`.
7. Find order by `PaymentCode == payload.code`.
8. Require order exists and uses `BankTransfer`.
9. Require `PaymentStatus == Pending`.
10. Require `transferAmount == order.Total`.
11. Save transaction log and set `PaymentStatus = Paid` in one save/transaction.
12. Return HTTP 200 with `{"success": true}`.

Rejected payloads return a non-success status only for authentication or malformed requests. Business mismatches are logged and return success if the transaction was safely recorded or intentionally ignored, so SePay does not retry forever on payments that cannot match an order.

## Configuration

Add settings under `SePay`:

- `WebhookSecret`
- `PaymentCodePrefix`, default `FTB`
- `BankAccountNumber`
- `BankCode`
- `AccountName`

Secrets stay outside source control in user secrets, environment variables, or production configuration.

## Error Handling

- Invalid HMAC or expired timestamp: return 401.
- Invalid JSON: return 400.
- Duplicate transaction id: return 200 success.
- Missing/unknown payment code: log warning, return 200 success.
- Amount mismatch: save transaction as `Ignored`, log warning, do not mark the order paid, return 200 success.
- Already paid order: log and return 200 success.

## Tests

Add focused tests only:

1. Bank-transfer order gets a unique `PaymentCode`.
2. Valid SePay webhook marks matching pending order as paid.
3. Duplicate webhook does not process twice.
4. Wrong amount does not mark order paid.
5. Invalid HMAC returns unauthorized.

## Out Of Scope

- Full payment status polling UI.
- Admin payment reconciliation dashboard.
- Partial payments.
- Refund automation.
- Multiple bank accounts per order.

Those can be added when the shop actually needs them.
