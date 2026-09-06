# MyPlanner — Domain Glossary

The vocabulary for the finance / receipt module. Terms are the project's
ubiquitous language; avoid the synonyms listed, which are either wrong or too
loose. This glossary starts with the billing domain resolved while designing
`BillReportPanel`; add terms here as other areas are pinned down.

## Bill

A transaction whose `dataOrigin` is `Receipt` — an OCR'd or manually-entered
shopping bill. A Bill owns a Merchant, a Timestamp, a Currency, a Total, free
text Notes, and a list of Line Items. This is the unit the user edits and
commits. A Bill is either **Provisional** (no bank row tied to it yet) or
**Reconciled** (merged with one).
_Avoid_: Invoice, expense, transaction (a Bill is one kind of transaction; the
generic backend entity is a Transaction).

## Merchant

The vendor or store name printed on a Bill. Optional on input; falls back to
"Unnamed bill" when absent.
_Avoid_: Vendor, shop, store (the receipt prints "Merchant" — keep that word).

## Timestamp

The instant the Bill occurred, stored as ISO-8601 and nullable (some receipts
have no date). Displayed compactly with 24-hour time.
_Avoid_: Date, receipt date (it carries time as well as date).

## Currency

The Bill's currency. One of `UAH`, `USD`, `EUR`.
_Avoid_: Money, price.

## Total

The Bill's grand total. While editing it is derived — the sum of each Line
Item's price × quantity — not an independent editable field.
_Avoid_: Grand total, amount, sum.

## Line Item

A single purchased entry on a Bill: a Name, a Quantity, a unit Price, the
derived Total Price, and a Category / Subcategory classification.
_Avoid_: Row, entry, product, item (a "row" is a UI line; the domain object is
a Line Item).

## Category / Subcategory

The classification of a Line Item. Category is the top-level group; Subcategory
is its child. Both optional.
_Avoid_: Type, tag, group.

## Draft

The set of staged, unsaved edits a Bill owns locally before the user commits.
Edits and line-item deletions live in the Draft and are applied only when the
user saves.
_Avoid_: Unsaved changes, temp data, local state.

---

# Reconciliation vocabulary

## Payment Method

Where the household's money sits or moves through: a Bank Card, a Savings Account, a Cash
Wallet, or Other. Only methods that hold a bank statement (cards, savings accounts) can be
imported into; a method with transactions pointing at it — in either direction — cannot be
deleted.
_Avoid_: Account, card (when cash is meant), wallet (except Cash Wallet).

## Bank Transaction

A Transaction whose `dataOrigin` is `Bank` — a row read from a bank statement. The
authoritative record that money moved: amount, timestamp, Payment Method, balance after,
MCC. Nothing else asserts that money left a card.
_Avoid_: Statement line, bank record (the entity is a Transaction with an origin).

## Reconciled

A Bill that has been merged with its Bank Transaction: one row carrying the bank's money
facts and the Bill's Line Items, `dataOrigin = Reconciled`. Because matched records merge,
a purchase can never be counted twice.
_Avoid_: Matched, linked, synced (matching is the act; reconciled is the state).

## Provisional Bill

A Bill with no Bank Transaction behind it yet. It counts as spend, but visibly unverified —
aging past 30 days it becomes a Review Item.
_Avoid_: Pending, temporary bill.

## Money Delta

`paid amount − sum of Line Items` on a Reconciled Bill. Shown, never hidden; the bank's
amount is always the ledger number and editing Line Items can widen the delta but never
changes what the bank says was paid.
_Avoid_: Difference, rounding error, mismatch.

## Matching / Suggestion

Matching is how a Bill and a Bank Transaction are recognised as one purchase: exact amount,
within ±1 hour, exactly one candidate pair. Anything wider or ambiguous is a **Suggestion**
the user confirms — never an automatic guess.
_Avoid_: Auto-link, dedupe (dedupe is about duplicate imports, not reconciliation).

## Review Item

Anything the system refuses to guess about, listed with severity and a suggested fix:
unmatched Bills, ambiguous matches, suspected duplicates, broken balance chains, invalid
categories. A dismissal persists as a rule so the same known case does not return.
_Avoid_: Error log, warning, notification.

## Cash Wallet

A `Cash` Payment Method: paper money the household physically holds. A household may have
several — one per Currency is the clean case — and nothing creates one automatically; you
make it. An ATM withdrawal is a Transfer into the Cash Wallet of the currency that was
dispensed, so cash leaving the bank appears once and cash spending becomes visible by
re-attributing an unmatched Bill to it.
_Avoid_: Cash account, petty cash, "the" household Cash Wallet (there is no single one).

## Needs Cash Target

The Review Item raised when a withdrawal's currency matches either no Cash Wallet or more
than one, so crediting the cash cannot be decided without asking. The fix is choosing — or
first creating — the Cash Wallet that now holds the money.
_Avoid_: Ambiguous wallet, unmapped withdrawal, cash mismatch.

## Bank Provider

The institution that issued a Payment Method and whose statement the ledger can read. A
closed choice in the UI: supported banks, or `Other / not listed`, which leaves it unset —
meaning **no statement format**, so the method can never receive an import. Only methods
that hold statements carry one.
_Avoid_: Bank name, provider string, treating "Other" as a bank.

## Household / Owner

One login owns the whole household ledger. Transfers between household members' cards are
internal movement, never spend. Household is not a User — there are no second logins in v1.
The person a Payment Method belongs to is carried in its **Name** (e.g. "Anna's card"): a
separate Owner field was deferred on 2026-09-07 and comes back only if per-person report
filtering needs it — see `.scratch/finance-reconciliation/issues/01-payment-methods-screen.md`.
_Avoid_: Family account, shared user, multi-user.

## Import Batch

The provenance record of one uploaded statement file: filename, content hash, target
Payment Method, rows read / inserted / skipped-duplicate / needs-review. Makes duplicate
uploads detectable and "undo this file" possible.
_Avoid_: Upload session, import log.

## Chain Verification

Per-Payment-Method proof that history is complete: `previous balance + amount = balance
after` for every consecutive pair, using the bank's own balance-after column. A break warns
loudly and never blocks the import.
_Avoid_: Reconciliation (that is Bill ↔ Bank), balance check (too vague).

## Coverage

The percentage of money in a total that is backed by Line Items. Printed beside totals so
"no receipt" is never mistaken for "nothing bought".
_Avoid_: Completeness, accuracy.
