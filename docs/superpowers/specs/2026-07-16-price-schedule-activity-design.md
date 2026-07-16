# Design: Quản lý giá & lịch hẹn đổi giá (Activity diagram)

**Date:** 2026-07-16  
**Status:** Draft for activity diagrams only (no implementation)  
**Feature slug:** `price-schedule`  
**Context:** Fruitables e-commerce (Product / ProductVariant already have `Price` + `SalePrice`)

---

## 1. Goal

Describe business flows for **variant price management** so the team can draw UML activity swimlanes:

- Adjust price immediately
- Schedule a future price change
- Cancel a pending schedule
- System applies due schedules

**Out of scope for this design:** coding, DB migrations, admin UI implementation, multi-product batch, manager approval workflow, percentage-based auto rules, external channel sync.

---

## 2. Decisions (locked)

| Topic | Decision |
|-------|----------|
| Price target | Each action targets **either** base `Price` **or** promo `SalePrice` (not both required at once) |
| Scope | **ProductVariant** (not product-only) |
| Actors | **Admin** + **System** |
| Schedule effect | Any change: raise / lower / set promo / clear promo |
| Pending schedules | **At most one** pending schedule per variant |
| Admin operations | Immediate change + create schedule + cancel pending |

---

## 3. Actors (swimlanes)

| Lane | Responsibility |
|------|----------------|
| **Admin** | Select variant, enter new price / time, confirm, cancel schedule |
| **System** | Show current prices & pending schedule; validate; persist; apply when due; mark schedule status |

---

## 4. Deliverable structure (4 activity diagrams)

Recommended: **one swimlane diagram per flow** (not one mega-diagram).

| ID | Name | Trigger | Primary end |
|----|------|---------|-------------|
| **A** | Immediate price change | Admin confirms “change now” | Variant price updated |
| **B** | Schedule price change | Admin confirms schedule | One pending schedule exists |
| **C** | Cancel pending schedule | Admin confirms cancel | No pending schedule |
| **D** | Apply due schedule | Scheduler / job at due time | Price applied; schedule = Applied (or Failed) |

**Suggested artifact names** (when drawing):

- `price-a-immediate-swimlane`
- `price-b-schedule-swimlane`
- `price-c-cancel-swimlane`
- `price-d-apply-swimlane`

**Style (align with existing chatbox diagrams):** filled initial ●, final ◎, decision diamond, merge when branches rejoin; lanes Admin | System; business-level action labels (not HTTP/API jargon).

---

## 5. Shared rules

1. **Valid price:** numeric value ≥ 0. Clearing promo = set `SalePrice` to empty/null only when a promo exists (or treat “clear” as explicit action).
2. **Schedule time:** must be **strictly in the future** when creating a schedule.
3. **One pending schedule:** if a pending schedule already exists when creating a new one, Admin must **replace** (cancel old + create new) or **keep** existing and exit.
4. **Orders:** already-placed order line prices are **not** changed by A or D.
5. **Immediate change vs pending schedule:** if Admin changes price now while a schedule is pending, System **warns**; default is still allow confirm (current price updates; pending schedule remains unless later product rule extends cancel-on-immediate).

---

## 6. Flow A — Immediate price change

| Step | Lane | Action |
|------|------|--------|
| 1 | Admin | Select product / variant |
| 2 | System | Show current base & promo prices |
| 3 | Admin | Choose target: base **or** promo |
| 4 | Admin | Enter new price (or clear promo) |
| 5 | System | Valid? If no → error → back to 3/4 |
| 6 | System | If pending schedule exists → warn Admin |
| 7 | Admin | Confirm immediate change |
| 8 | System | Update variant price field |
| 9 | System | Optional: record “manual change” note on diagram |
| 10 | Admin | See updated price → end |

---

## 7. Flow B — Schedule price change

| Step | Lane | Action |
|------|------|--------|
| 1 | Admin | Select product / variant |
| 2 | System | Show current prices + pending schedule if any |
| 3 | System | Pending exists? |
| 3a | Admin | If yes: **Replace** or **Keep / exit** |
| 3b | — | Keep → end (no change) |
| 4 | Admin | Choose target: base **or** promo |
| 5 | Admin | Enter new price (or clear promo) |
| 6 | Admin | Choose apply-at datetime (future) |
| 7 | System | Valid price + future time? If no → error → fix 5/6 |
| 8 | Admin | Confirm schedule |
| 9 | System | Save **one** pending schedule (status: Pending); replace prior pending if Replace |
| 10 | Admin | See confirmation → end |

Actual price write happens only in **Flow D**.

---

## 8. Flow C — Cancel pending schedule

| Step | Lane | Action |
|------|------|--------|
| 1 | Admin | Select product / variant |
| 2 | System | Load pending schedule |
| 3 | System | Exists? If no → message → end |
| 4 | System | Show summary (target, new price, apply-at) |
| 5 | Admin | Confirm cancel? If no → end (keep schedule) |
| 6 | System | Mark schedule **Cancelled** / remove from pending |
| 7 | Admin | See “cancelled” → end |

**Note:** Current shelf price is unchanged.

---

## 9. Flow D — System applies due schedule

| Step | Lane | Action |
|------|------|--------|
| 1 | System | Load pending schedules with apply-at ≤ now |
| 2 | System | Any left? If no → end (idle) |
| 3 | System | Take next schedule |
| 4 | System | Variant still exists & usable? If no → status **Failed** → go to 2 |
| 5 | System | Apply price (base / promo / clear promo) |
| 6 | System | Status **Applied** |
| 7 | System | Optional: audit “applied by schedule” |
| 8 | — | Go to 2 |

Admin is not required on this diagram (may view result later on price screen).

---

## 10. Schedule statuses (for labels / optional state diagram)

| Status | Meaning | Set by |
|--------|---------|--------|
| **Pending** | Waiting for apply-at | B |
| **Applied** | Price written successfully | D |
| **Cancelled** | Admin cancelled before apply | C or B (replace) |
| **Failed** | Due but could not apply | D |

---

## 11. Decision / error summary

| Situation | Flow | Handling on activity |
|-----------|------|----------------------|
| Invalid price | A, B | Error → re-enter |
| Clear promo with no promo | A, B | Error → re-choose |
| Apply-at not in future | B | Error → re-pick time |
| Pending already exists | B | Replace or keep/exit |
| No pending | C | Message → end |
| Admin aborts confirm | A, B, C | End without change |
| Variant missing/inactive | D | Failed; continue other schedules |
| Immediate change with pending | A | Warn; allow confirm |

---

## 12. Relationship between flows

```
A  Immediate ──────────────────────────► current price
B  Schedule ──(Pending)──► D Apply ───► current price
C  Cancel ────► no Pending (D has nothing for that variant)
```

---

## 13. Success criteria (for diagram work)

- Four activities cover A–D with Admin | System swimlanes.
- Every branch reaches a final or clear loop-back.
- Labels are business-level (no API/HTTP detail required).
- “One pending schedule” and “apply only in D” are visible in B/D decisions.

---

## 14. Next steps (after this spec is approved)

1. Draw four activity swimlanes (PlantUML or StarUML-style HTML/SVG under `docs/`, e.g. `docs/price-schedule/srs/`).
2. If product later wants implementation: separate brainstorm/plan for data model (`PriceSchedule` entity), job, and admin UI — **not** part of this document’s delivery.
