# GHN Package Sizing Design

## Goal

Use real cart quantity to calculate GHN package weight and box dimensions instead of sending one fixed package size for every order.

## Current Context

- The shop sells fruit by kilogram.
- Cart item `Quantity` represents kilograms purchased.
- `Product.Weight` already exists, but it should not be used for GHN shipping in this flow because quantity already carries the weight.
- GHN requires package `weight`, `length`, `width`, and `height`.
- Manual zone-based shipping fallback should not be used.

## Shipping Package Rules

Calculate total package weight from the cart:

```text
totalKg = sum(cart item quantities)
weightGram = totalKg * 1000
```

Choose the package box from the total kilograms:

| Total kg | Box size |
| --- | --- |
| `<= 2kg` | `20 x 15 x 10 cm` |
| `<= 5kg` | `30 x 20 x 15 cm` |
| `> 5kg` | `40 x 30 x 20 cm` |

For example:

- `2kg` apples -> `2000g`, `20 x 15 x 10`
- `4kg` oranges -> `4000g`, `30 x 20 x 15`
- `6kg` mangoes -> `6000g`, `40 x 30 x 20`

## Data Flow

1. Cart loads items with product, price, and quantity.
2. Cart summary derives package dimensions from cart quantities.
3. Checkout passes GHN address codes and derived package data to shipping calculation.
4. Shipping calculation calls GHN with:
   - `to_district_id`
   - `to_ward_code`
   - `weight`
   - `length`
   - `width`
   - `height`
5. If GHN returns a fee, show and persist that fee.
6. If GHN cannot calculate a fee, show the GHN failure message and do not fall back to manual zone fees.

## Implementation Shape

Keep this small:

- Add package fields to the cart/shipping path only where needed.
- Derive package data from existing cart items.
- Keep box rules as code constants for now.
- Do not add per-product dimensions.
- Do not use `Product.Weight` for this GHN package calculation.

## Error Handling

- Empty cart or zero subtotal should not call GHN.
- Missing GHN address codes should return the existing GHN failure state.
- GHN API failure should return the existing GHN failure state.
- No manual fee fallback should run.

## Tests

Add focused tests for:

- `1kg` uses `1000g` and the small box.
- `2kg` uses `2000g` and the small box boundary.
- `3kg` to `5kg` uses medium box.
- `>5kg` uses large box.
- GHN receives derived package values instead of default config values.
- GHN failure does not fall back to manual zone shipping.
