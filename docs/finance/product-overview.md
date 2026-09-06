# Personal Finance Ledger — Product Overview

A household money ledger for a Ukrainian family: bank statements are the truth about
**money that moved**, shopping bills are the truth about **what was bought**, and the
app's only real job is to tie those two documents together so you can trust the numbers.

The visible payoff is understanding food spending — where it goes, which market, what
it actually cost — inside a ledger that covers all spending.

---

## The core problem

Two sources of information about the same purchase exist and they disagree:

| Source | Knows | Does not know |
| --- | --- | --- |
| **Bank export** (Monobank CSV) | exact amount, exact time, which card, balance after, MCC code | what you bought — at best a till-point name like "NOVUS", often an opaque one like "SILPO, branch #5" |
| **Shopping bill** (photo / PDF, OCR'd) | Merchant, Line Items, quantities, unit prices, categories | how it was paid, whether the money actually left a card |

Upload both without a reconciliation rule and every shopping trip becomes two expenses.
Sum receipts instead of bank rows and totals silently ignore 95% of card spending that
has no receipt. Either mistake makes the report worthless within a month.

## The core idea

**A bank transaction is the authoritative record of money movement. A Bill is detail.**

When a Bill and a bank transaction are recognisably the same purchase, they become **one
row**: the Bill's row absorbs the bank facts (card, balance after, exact amount) and is
marked **Reconciled**. Because matched records merge rather than coexist, counting both
sides of a purchase is structurally impossible — not a bug you have to remember to avoid.

Everything else in the product follows from that: reports sum rows once, unmatched Bills
are provisional-but-counted, and anything ambiguous is shown as a question instead of an
answer.

---

## Main concepts

### Money Truth
Money movement is only ever asserted by a bank row or by a Bill that has no bank twin
yet. Reports never mix "receipt total" and "paid amount" in the same sum without saying
which one they are showing.

### Bill / Provisional Bill
A Bill is created the moment you upload a photo of a receipt — you do not wait for the
statement. Until it merges with a bank row it is **provisional**: it counts as spending,
but it is flagged, because nothing yet proves the money left a card. A provisional Bill
older than 30 days becomes a Review Item ("bill with no money behind it").

### Reconciliation
The merge of a bank transaction into an existing Bill row. Result: `DataOrigin.Reconciled`.
Both raw source payloads (the CSV line, the model's JSON) are kept on the merged row, so
a wrong auto-match can always be un-linked back into two rows.

### Money Delta
`paid amount − sum of Line Items`. Shown, never hidden. A non-zero delta means OCR
dropped an item, a loyalty discount applied, or the match is wrong — and it is the only
honest way to say "line items explain 249.30 of the 251.00 that left your card". The
bank amount is always the ledger number; editing Line Items can create a delta but never
changes what the bank says you paid.

### Matching
Exact amount **and** within ±1 hour **and** exactly one candidate on each side → auto-match.
Wider than that (±12 hours, up to ~5% amount drift for discounts) or ambiguous (two
candidate pairs) → a *suggestion* you confirm. The matcher runs in both directions on
every ingestion: a new bank row looks for Bills, a new Bill looks among existing rows.
A "re-match everything unmatched" action is always available — it is the repair tool for
after the matcher improves.

### Household / Owner
One login owns the whole household ledger. **Owner labels are deferred (2026-09-07, ticket
01):** the person a card belongs to is carried in its Name for now ("Anna's card"), so filtering
reports by person is a naming convention rather than a field. A transfer between your card and hers
is internal money movement, never spending — which is exactly the double-counting case a
shared household creates, and why household ≠ user.

### Cash Wallet
A `Cash` payment method — paper money you physically hold. You create them by hand and a household
may have several (one per currency is the clean case); nothing mints one for you. An ATM withdrawal
is recorded as a Transfer **into** the wallet of the currency the ATM dispensed, so cash leaving the
bank appears once; cash spending becomes visible by re-attributing an unmatched Bill to the wallet
instead of deleting it. Each wallet can be chain-checked like a card. When the currency matches zero
wallets or several, nothing is guessed: the withdrawal raises a **Needs Cash Target** review item and
you pick (or create) the wallet.

### Bank Provider
The institution that issued a payment method and whose statement format the ledger can read. You pick
it from a closed list — Monobank today, plus `Other / not listed`. Choosing Other leaves it unset,
which means *no statement format*: the card simply cannot be chosen as an import target until you set
one. A provider is never guessed at import time.

### Import Batch
Every uploaded file is recorded: filename, content hash, target Payment Method, rows
read / inserted / skipped-duplicate / needs-review, timestamp. This is what makes
double-uploading a statement detectable, and what makes "undo this file" possible.

### Chain Verification
Within one card's history, `previous balance + amount = balance after` must hold for
every consecutive pair, using the bank's own `Залишок після операції` column. A break
means rows are missing or were imported twice — your totals for that card are wrong and
you don't know how much. Imports are **never blocked** by a broken chain; they insert the
data and raise a high-severity Review Item, because blocking destroys the evidence needed
to find the gap.

### Natural-key dedupe
Monobank's CSV has no transaction ID column. Identity is therefore the composite
`(Payment Method, Timestamp, Amount, Balance After)`, enforced by a unique index — not an
in-memory comparison that quietly drops rows with no timestamp.

### Review Item
Anything the system refuses to guess about, listed with severity and a suggested fix.
Fix vocabulary: confirm suggested match · un-link wrong match · attach Bill to bank row
manually · mark duplicate · merge two transactions · mark as transfer (pick the other
card) · pick which Cash Wallet a withdrawal credited · re-categorize · re-attribute Bill to Cash Wallet · dismiss with reason. A
dismissal persists as a rule so a known-expected item stops nagging every month.

### Coverage
The percentage of money backed by Line Items. Displayed next to every total, and rising
over time as Bills are uploaded. It replaces the lie that "no receipt" means "nothing
bought" — 90% of this household's spending happens at three markets, so detail coverage is
the number that tells you how much of your food report is real.

### Category / Subcategory
The existing curated taxonomy (11 categories with subcategories) is the single source of
truth. The model must answer inside that set; an invalid answer is rejected and retried,
never stored as free text. MCC from the bank export is a *fallback prior* used only when a
row has no Line Items — never an override of what was actually read off the Bill.

---

## Invariants (rules that must never break)

1. One purchase produces exactly one row in spend totals.
2. The amount in a total is the amount the bank says moved, when the bank knows.
3. Transfers between own payment methods — including into the Cash Wallet and between
   household members' cards — are excluded from every spend aggregate by construction.
4. Nothing is ever auto-matched when more than one candidate exists.
5. No import silently replaces data; every file has a Batch, every duplicate is counted.
6. A total is always accompanied by its coverage and its open Review Item count.
7. Deleting a Bill's raw payload or a bank row's raw payload is not required to un-link a
   match — provenance survives the merge.

---

## What you actually look at

- **Dashboard**: spending matrix by Category × Subcategory for a week / month / custom
  range, with drill-down into Line Items; per-total coverage % and an error indicator
  leading to the Review queue.
- **Bank**: statement import — pick the card a CSV belongs to, see its date range and row count,
  confirm. Payment-method management sits behind it on `/payment-methods` (not in the sidebar).
- **Payment methods screen** (`/payment-methods`): cards, savings accounts and cash wallets, soft-grouped
  in that order, each with chain-verification status (green tie-out is the signal that lets you trust
  every other number). Owner labels deferred with the field.
- **Review queue**: unmatched Bills, ambiguous matches, suspected duplicates, broken
  balance chains — each with a one-click fix.
- **Transaction list**: filtered by date range (by person once Owner lands), showing Reconciled / Provisional
  state and Money Delta per row.

First implementation aggregates in the browser over `GET /finance/transactions` (the
existing endpoint already accepts a date range); the counting rule itself comes from the
backend as a derived field so no client invents its own arithmetic. Server-side
aggregation is phase 2, once the data model has settled.

---

## Explicitly not this product

Budget limits and overspend alerts · forecasting · investment or savings performance
tracking · subscription/recurring-payment detection (needs 3+ months of clean data to be
trustworthy) · canonical Merchant entities · product identity resolution and price-watch
lists across OCR variants · Open Banking / PSD2 live sync · bank PDF statement parsing ·
mobile camera capture · shared logins for the household · several Bills behind one payment.

---

## Assumptions applied on your behalf

Chosen because you said to apply my suggestions; veto any single one and only its branch
changes.

1. Merged row keeps the **bank amount** as the ledger number, with Money Delta shown.
2. Raw payloads of both sources are stored so un-linking is possible.
3. Multi-Bill-per-payment (N:1) is out of v1; the leftover Bill becomes a Review Item.
4. Matching runs in both directions and may cross household cards; when Owner returns it comes
   from the card that matched.
5. `CountsInTotals` (or equivalent derived role) is computed by the backend, summed by the
   frontend in v1.
6. One uploaded file = one Payment Method, chosen manually, confirmed with date range and
   row count before insert; filename auto-detection rejected as brittle.
7. Bank history is backfilled (6–12 months); Bills are forward-only unless digital
   receipts can be dumped as a folder of PDFs, in which case a one-off script ingests them.
8. Household members are data-subjects, not users; no second login in v1.
9. The merged state is named `Reconciled` (not `BankAndReceipt`).
10. Existing finance test data is wiped rather than migrated, since legacy rows carry no
    provenance and would pollute the first real totals.
11. Build order: import correctness (batches, dedupe, chain verification, review queue)
    lands with the category/subcategory dashboard in the same pass — not behind a
    correctness epic.
12. Foreign-currency Bills with no bank rate get a UAH base amount from the NBU rate for
    the Bill's date; unknown-rate rows are flagged rather than summed at face value.
