# Domain design

What this system is for, how the model is shaped, and why. Written before the code so the reasoning
survives; update it when a decision changes.

## The problem

Amateur poker nights are organised on paper and in group chats. A buy-in and rebuy amount get agreed,
someone decides what a chip is worth in real money, rebuys are remembered out loud, and at the end
everyone counts chips, works out who owes whom, and sends the money. One person keeps the
ranking by hand.

Three things hurt:

1. **The chip case.** Handing out a balanced stack, and knowing what is left in the case. The small
   denominations always run out first.
2. **Closing the table.** Making the final count actually reconcile against what left the case, then
   settling up in as few payments as possible.
3. **The ranking.** Keeping it fair across a whole year without a spreadsheet.

The system does not deal cards or run betting rounds. It is the bookkeeper.

## Shape of the model

Entities live in `backend/src/Domain/`, following the repo's existing style: plain entities deriving
from `Entity`, static error factories in `<Aggregate>Errors.cs`, domain events where something else
has to react.

### Identity and championship

- **`User`** — plus `DisplayName` (what shows at the table and in rankings) and an optional generic
  payment handle (`PaymentType` + `PaymentHandle`; Pix is the default type). Generic rather than a
  hardcoded Pix key: it costs nothing and keeps the settlement report usable elsewhere.
- **`Championship`** — owner, name, and the defaults: buy-in, rebuy, whether those defaults are
  suggested or enforced, money-per-unit, the points table, the default blind structure.
- **`ChampionshipMember`** — user + role. Ordered: `Owner(3) > Admin(2) > TableManager(1) >
  Player(0)`. A member may only change roles **strictly below** their own; transferring ownership is
  its own operation, Owner to Admin.
- **`Invite`** — a 6-character code from an alphabet without `0/O/1/I`, with expiry and a use count.
  No email is involved, so no SMTP to provision.
- **The championship is the season.** A new year means a new championship, so the
  ranking window needs no separate concept: it is simply everything played in
  this championship. A `Season` entity existed briefly and was removed — it
  carried a date range, an overlap rule and a screen, all to express something
  the championship boundary already expressed.

### Chip case

- **`ChipSet`** — a case belonging to a championship.
- **`ChipDenomination`** — `FaceValue` (what is printed on the chip), `EffectiveValue` (what it counts
  as in play — this is what lets a 5 chip be treated as 100), `Quantity`, colour, label.

Money is never stored on the chip. It comes from the table: `money = units × MoneyPerUnit`. With
`MoneyPerUnit = 0.05`, a R$ 50 buy-in is 1000 units. This keeps three things apart that otherwise get
confused: what is printed, what it counts as, and what it is worth in cash.

### Table

- **`Table`** — championship, chip set, buy-in, rebuy, money-per-unit, join policy
  (`AnyMember | InviteOnly | Code`), whether late entry is allowed, blind structure, status.
- Status runs `Draft → Open → Running → Counting → Reconciled → Settled → Closed` (plus `Cancelled`).
  **`Reconciled` is the gate**: it is only reachable once the count matches, and only from there can
  a settlement be produced.
- **`TablePlayer`** — `Standby | Playing | Left`, seat order, joined-at.
- **`BlindStructure` / `BlindLevel`** — order, small blind, big blind, ante, duration.
- **`TableClock`** — current level, `LevelStartedAtUtc`, `PausedAtUtc`, accumulated pause. The server
  stores **timestamps, never a counting-down number**: each phone computes the remainder itself, so
  the clock stays correct on every device without the polling having to be precise.

### Ledger

- **`LedgerEntry`** — player, type, money amount, optional counterparty.
  Types: `BuyIn`, `Rebuy`, `ChipPurchaseFromPlayer`, `ChipSaleToPlayer`, `Adjustment`.
- **`LedgerEntryChip`** — the per-denomination quantities for an entry. **Only exists when chips
  actually left the case.** A purchase between players produces none: those chips were already in
  play, they just changed hands.
- **`LedgerEntry.AcknowledgedAtUtc`** — when the player confirmed they were actually handed those
  chips. Null means the notice is still queued for them. Only the player themselves may set it: the
  whole value of the check is a second pair of eyes, and the manager already counted the stack out.
- **`FinalCount`** — per player, per denomination, reported by the player themselves.
- **`Settlement` / `SettlementTransfer`** — from, to, amount. Generated once, then immutable.
- **`TableResult`** — position, points, balance, written at close. Both rankings are aggregations over
  this table; nothing is recomputed from the ledger to display a ranking.

### The arithmetic

```
PaidIn(p)  = Σ(BuyIn + Rebuy + ChipPurchaseFromPlayer) − Σ(ChipSaleToPlayer)
Chips(p)   = Σ(FinalCount.Quantity × EffectiveValue) × MoneyPerUnit
Balance(p) = Chips(p) − PaidIn(p)
```

Two invariants are checked before the table may close:

1. **Per-denomination reconciliation** — for every chip, `Σ LedgerEntryChip.Quantity ==
   Σ FinalCount.Quantity`.
2. **Zero sum** — `Σ Balance(p) == 0`. It follows from (1), but it catches rounding mistakes.

**Running out of chips** is handled as a purchase between players, and it closes on its own: A pays
the rebuy (A's `PaidIn` goes up), B hands over chips and is credited the same amount (B's `PaidIn`
goes down). No chips left the case, so reconciliation still holds, and B is not punished for bailing
the table out.

## Algorithms

Pure classes in `backend/src/Domain/Tables/Services/`, no EF dependency, tested directly.

**`ChipDistributionCalculator`** — takes a unit target, the *current* stock, and a profile
(percentage of total value per denomination). Converts the profile to target counts, clamps each by
what is in stock, and covers the shortfall by moving up to the largest available denomination. It has
to hit the target **exactly**; if the remaining stock cannot, it reports the deficit rather than
rounding. Because it always works from current stock, the behaviour everyone expects — small chips
run out, so later stacks come in bigger denominations — falls out without a special rule. An optional
per-table small-chip reserve holds some back for the first rebuys.

**`TableReconciliationService`** — returns issued / counted / difference per denomination, plus who
has not reported yet. Shown live during `Counting`, so the table can see *which* chip is off instead
of recounting everything.

**`SettlementCalculator`** — minimising transfers is NP-hard in general, but a poker table is small.
Up to 15 players: bitmask DP partitioning into zero-sum subsets, each settled in (size − 1)
transfers — genuinely optimal. Beyond that: greedy largest-debtor to largest-creditor, at most n−1
transfers. One person paying several others is expected and correct.

**`TableResultCalculator`** — orders by balance descending (ties broken by lower `PaidIn`, then join
time), applies the championship's points table, writes `TableResult`. Two rankings over the
championship: total points, and total balance. Both are sums over `TableResult` and nothing else —
the night's numbers were frozen when it settled, and people have already paid each other on the
strength of them, so recomputing from the ledger could contradict money that has moved.

## Authorization

The template's `PermissionProvider` is global, and roles here are per championship — the same person
is Owner in one and Player in another. So championship-scoped authorization resolves
`(userId, championshipId) → Role` and is applied as an endpoint filter reading the championship id
from the route, cached briefly via `HybridCache`.

## Decisions

| Decision | Why |
|---|---|
| Polling, not SignalR/SSE | Avoids WebSocket through the Cloudflare Tunnel and session affinity across 2 API replicas. The game moves at human speed. |
| Chips-from-player as a purchase | Reconciliation keeps working and the lender is not out of pocket. A separate debt type would fork the accounting. |
| Configurable points table | Every group scores differently; a fixed formula would just be a guess. |
| Blind levels with a shared clock | Home games grow into needing it, and it kills the "someone shouts the time" problem. |
| Invite by link + short code | Works in a group chat, needs no SMTP. |
| i18n from the start (pt source, en translation) | Retrofitting i18n means touching every template again. |
| Generic payment handle | Pix by default, but nothing in the model is Brazil-only. |
| Enums as strings over the wire | Reordering an enum member stops being a silent breaking change. |
| Stack notices queued on the table payload | The notice is already waiting when the player opens the screen, and reaches whoever was not looking at their phone when the table started. A push would need infrastructure this does not have. |

## Delivery phases

Each phase ends with something usable end to end — backend, screen, tests.

- **Phase 0 — Foundation.** *Done.* Sample slice removed; `DisplayName` and payment handle on `User`;
  Angular shell with Material, i18n and the auth screens.
- **Phase 1 — Championship.** *Done.* Championships, members and roles, ownership transfer, invites,
  chip cases and denominations, the points table. Championship-scoped authorization.
- **Phase 2 — Table in play.** *Done.* Opening a table, joining, starting with a calculated
  distribution and stock deduction, rebuys against current stock, purchases between players, blind
  levels and a shared clock.
- **Phase 3 — Closing.** *Done.* Player-reported counts, the live reconciliation panel, the
  `Reconciled` gate, settlement and table result.
- **Phase 4 — Rankings.** *Done.* Both rankings over the championship, table history, personal
  statement, championship statistics.

## Open, not blocking

- Timestamps stored in UTC, converted for display (default `America/Sao_Paulo`).
- No PWA/offline support planned yet. If phone signal at the table turns out to be a real problem, it
  would be read caching plus a write queue.
- No photo/OCR of chips. Counts are typed in.
