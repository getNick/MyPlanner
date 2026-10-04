# MyPlanner — Domain Glossary

The vocabulary for the finance / receipt module. Terms are the project's
ubiquitous language; avoid the synonyms listed, which are either wrong or too
loose. This glossary starts with the billing domain resolved while designing
`BillReportPanel`; add terms here as other areas are pinned down.

## Bill

A saved shopping receipt with a Merchant, confirmed Timestamp, Currency, Total,
Notes, and Line Items. A Bill is either **Provisional** (no Bank Transaction tied
to it yet) or **Reconciled** (merged with one).
_Avoid_: Invoice, expense, transaction (a Bill is one kind of transaction; the
generic backend entity is a Transaction).

## Transaction

The generic ledger record of money movement. A Transaction may originate from a Bank statement, a Bill, or manual entry; use the specific term (Bank Transaction, Bill, or Manual Transaction) when its origin matters. A Reconciled Bill and its Bank Transaction are one ledger Transaction, not two.
_Avoid_: treating every Transaction as a Bill, or counting a Reconciled purchase twice.

## Manual Transaction

A ledger Transaction entered directly by a user rather than imported from a Bank statement or created from a shopping Bill. Manual entry records a description, amount, and Timestamp, with one Line Item of the same amount classified when known. It is not a Bill.
_Avoid_: using manual dashboard entry as a substitute for the Shopping Bills workflow.

## Merchant

The vendor or store name printed on a Bill. Optional on input; falls back to
"Unnamed bill" when absent.
_Avoid_: Vendor, shop, store (the receipt prints "Merchant" — keep that word).

## Timestamp

The instant a Transaction occurred. A saved Bill or Manual Transaction has a
confirmed Timestamp; an unsaved Bill Draft may lack one until the user supplies it.
_Avoid_: Date, receipt date (it carries time as well as date).

## Currency

The Bill's currency. One of `UAH`, `USD`, `EUR`.
_Avoid_: Money, price.

## Total

The Bill's grand total. While editing it is derived — the sum of each Line
Item's price × quantity — not an independent editable field.
_Avoid_: Grand total, amount, sum.

## Line Item

A purchased entry on a Bill, or the single detail of a Manual Transaction:
Name, Quantity, unit Price, Total Price, and optional Category / Subcategory.
_Avoid_: Row, entry, product, item (a "row" is a UI line; the domain object is
a Line Item).

## Category / Subcategory

The classification of a Line Item. Category is the top-level group; Subcategory
is its child. Both optional.
_Avoid_: Type, tag, group.

## Draft

Staged, unsaved edits to an existing Bill. Applied only when the user saves.
_Avoid_: Bill Draft (a new Bill awaiting confirmation), temp data.

## Bill Draft

An unsaved receipt image and editable OCR result, held until the user confirms
the Timestamp and saves a Bill. It does not count as spending.
_Avoid_: Provisional Bill (already saved), Draft (edits to an existing Bill).

## Receipt Evidence

The privately stored original receipt image backing a saved Bill, distinct from
its extracted Line Items. It remains associated with the Bill after reconciliation.
_Avoid_: OCR result, public image link.

---

# Reconciliation vocabulary

## Payment Method

Where the household's money sits or moves through: a Bank Card, a Savings Account, a Cash
Wallet, or Other. Only methods that hold a bank statement (cards, savings accounts) can be
imported into; a method with transactions pointing at it — in either direction — cannot be
deleted.
_Avoid_: Account, card (when cash is meant), wallet (except Cash Wallet).

## Bank Transaction

A Transaction read from a bank statement. The authoritative record of card
money movement: amount, Timestamp, Payment Method, balance after, MCC. A Bill
without a Bank Transaction is provisional; manual entry asserts money movement
by the person, not by the bank.
_Avoid_: Statement line, bank record (the entity is a Transaction with an origin).

## Reconciled

A Bill that has been merged with its Bank Transaction: one row carrying the bank's money
facts and the Bill's Line Items. Because matched records merge,
a purchase can never be counted twice.
_Avoid_: Matched, linked, synced (matching is the act; reconciled is the state).

## Provisional Bill

A saved Bill with no Bank Transaction behind it yet. It counts as spend, but
visibly unverified; its Payment Method may be Unknown until matching. Aging
past 30 days it becomes a Review Item.
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

## AmountUah

A Transaction's amount expressed in UAH for household totals, backed by a bank
UAH figure or a dated conversion. When no trustworthy rate exists, it is unknown;
the original Amount and card-currency BaseAmount are not interchangeable with it.
_Avoid_: BaseAmount, original-currency Amount.

## Transaction Kind

The form of a ledger Transaction: Bank, Receipt (Provisional Bill), Manual, or
Reconciled. Reconciled describes a combined row, not a separate data source.
_Avoid_: Data Origin (suggests all four values name sources).

## Coverage

The percentage of money in a total that is backed by actual Line Items rather
than bank MCC classification alone. Printed beside totals so "no receipt" is
never mistaken for "nothing bought".
_Avoid_: Completeness, accuracy.

# Stored source vocabulary

Design: `docs/receipt-raw-storage-design.md`. These name what the ledger keeps *behind* a row —
the bytes it was extracted from. Nothing is read back into the product yet; the corpus exists so
extraction can be re-run once models or parsers improve.

## Bill Image

The photo of a shopping Bill that OCR was given for a Provisional Bill; the bytes a future OCR
improvement must be run against. Stored verbatim under its content hash, never edited.
_Avoid_: Receipt file, scan, attachment.

## Statement File

The bank export exactly as uploaded (CSV today; Excel, PDF or a photo possible later), kept
verbatim in the Bucket under its content hash. One import stores one file, however many rows it
yields — and re-importing the same bytes stores it once.
_Avoid_: Bank file, CSV upload, statement source.

## Bucket

The single flat folder where every stored upload lands, named by the `STORAGE_PATH` env var (code
default `./storage`) and bind-mounted into the api container. Files are distinguished only by
extension, never by subfolder.
_Avoid_: Storage root with subfolders, `receipts/` and `statements/` folders, uploads directory.

## FileKey

The value written into the envelope that resolves to a stored file in the Bucket:
`<sha256-hex>.<ext>` — the whole filename, and the only handle anyone holds for a stored file.
_Avoid_: File path, filename (as a concept), blob id.

## Raw Transaction Data Envelope

The JSON object `Transaction.RawTransactionData` carries from now on: one nested object per
origin — `bill` (the Bill Image's FileKey) and `bank` (Statement File key, row number, profile) —
so a Reconciled row holds both after its Bank Transaction is deleted. Older rows may hold a bare
statement line or nothing; readers tolerate all three.
_Avoid_: Raw json, metadata blob, extra fields.

## Replayable Bank Row

A Bank Transaction whose stored raw data lets the same parser extract it again: the Statement File
plus the row's line number and Bank Profile, not the bare data line alone. Today's column holds
the line only, which is why it dies.
_Avoid_: Raw record, CSV string, source row.
