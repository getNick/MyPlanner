import { bankClassification, isAllocation, money } from "./transactionDetail";
import type { BackendTransaction } from "../types/receiptTypes";

/**
 * The dashboard's report summing, pulled out of the 1300-line `DashboardPage` so it can be reasoned
 * about and tested on its own — the same seam `domain/bankTransactions.ts` and `domain/paymentMethods.ts`
 * give the `/finance/bank` and `/finance/payment-methods` screens. The page feeds it transactions and gets report
 * numbers back; it never sums a row inline again, so the visible and future slices land on one
 * idea of "the report".
 *
 * The rule this function exists to enforce is counting. Every ledger row is counted exactly once:
 * a reconciled row *is* the purchase — the Bill and its Bank Transaction merged into it — so it
 * carries its Line Items and is summed one time, never dropped (zero) and never also summed under a
 * separate Bank row (twice).
 */

/**
 * The report's date window. Both bounds are ISO dates (year-month-day); `null` is "no bound on that
 * side", so `{ from: null, to: null }` is the whole ledger. Bounds are day-inclusive, so a row and
 * the day it falls in are never split by rounding.
 */
export interface ReportRange {
  from: string | null;
  to: string | null;
}

/** category → the line-item prices of every counted row in range, summed into it. */
export type CategoryMatrix = Record<string, number>;

/**
 * Money Delta rollup: `paid − sum of Line Items` summed across the counted rows. Money Delta is the
 * bank's figure minus what the paper explained (CONTEXT.md: *Money Delta*); it is only a fact where
 * a bank amount can be set against line items, which is a reconciled row.
 */
export interface MoneyDeltaRollup {
  /** Sum of per-row Money Delta over the counted reconciled rows. */
  total: number;
  /** How many counted rows carried a money delta. */
  rows: number;
  /** Per-Reconciled-row Money Delta, for showing the value beside each ledger row. */
  byTransaction: { id: string; description: string; amount: number }[];
}

/** The report's numbers: what was spent per category, and the Money Delta rollup. */
export type CategorySubcategoryMatrix = Record<string, Record<string, number>>;

export interface ReportSum {
  categoryMatrix: CategoryMatrix;
  categorySubcategoryMatrix: CategorySubcategoryMatrix;
  total: number;
  estimatedMatrix: CategorySubcategoryMatrix;
  coverage: number;
  moneyDelta: MoneyDeltaRollup;
}

/**
 * Whether the row's timestamp falls in range. An undated row — a receipt with no date, which the
 * import holds back rather than drops — is included, because a row the report hides is money
 * uncounted; the report counts it, it never loses a row to a missing date.
 */
function inRange(timestamp: string | null | undefined, range: ReportRange): boolean {
  if (!timestamp) return true;

  const when = new Date(timestamp).getTime();
  if (Number.isNaN(when)) return true;

  if (range.from) {
    const from = new Date(`${range.from}T00:00:00`).getTime();
    if (when < from) return false;
  }

  if (range.to) {
    const to = new Date(`${range.to}T23:59:59.999`).getTime();
    if (when > to) return false;
  }

  return true;
}

/** The line-item total of one row — what the paper explained, for the category matrix and the delta. */
function lineItemTotal(row: BackendTransaction): number {
  return row.items.reduce((sum, item) => sum + item.totalPrice, 0);
}

/**
 * Turn the ledger into the report's numbers: a category matrix and a Money Delta rollup.
 *
 * Each row in range is counted exactly once. Its Line Items feed the category matrix; for a
 * reconciled row the bank's `amount` set against those line items is its Money Delta. Owner filtering
 * is deferred (ticket 03): the parameter keeps the seam's signature stable, and nothing is filtered
 * yet, so a row is never dropped by an owner the caller has not chosen.
 *
 * @param transactions the ledger rows (Bank, Receipt, Manual, and Reconciled) — one entry per row.
 * @param range the inclusive date window; both bounds null means the whole ledger.
 * @param owner reserved for per-owner filtering; accepted now so the signature is stable, ignored.
 */
export function summarizeReport(
  transactions: BackendTransaction[],
  range: ReportRange,
  owner: string | null = null,
): ReportSum {
  // Owner filtering is deferred: an owner id would select the rows whose Payment Method it names.
  // Nothing is filtered yet, so this row count is every row in range — an owner is never a reason to
  // drop one.
  void owner;

  const categoryMatrix: CategoryMatrix = {};
  const categorySubcategoryMatrix: CategorySubcategoryMatrix = {};
  let total = 0;
  let coveredPaidTotal = 0;
  let paidTotal = 0;
  const estimatedMatrix: CategorySubcategoryMatrix = {};
  let moneyDeltaTotal = 0;
  let moneyDeltaRows = 0;
  const moneyDeltaByTransaction: MoneyDeltaRollup["byTransaction"] = [];

  const addSlice = (category: string, subcategory: string, amount: number, estimated: boolean) => {
    categoryMatrix[category] = (categoryMatrix[category] || 0) + amount;
    categorySubcategoryMatrix[category] ??= {};
    categorySubcategoryMatrix[category][subcategory] = (categorySubcategoryMatrix[category][subcategory] || 0) + amount;
    total += amount;
    if (estimated) {
      estimatedMatrix[category] ??= {};
      estimatedMatrix[category][subcategory] = (estimatedMatrix[category][subcategory] || 0) + amount;
    }
  };

  for (const row of transactions) {
    if (row.type !== "Expense" || !inRange(row.timestamp, range)) continue;

    // An imported Bank row has classification-only generated detail. Use it only when no real
    // Line Items exist; never mix that placeholder with receipt/manual detail.
    const realItems = row.items.filter((item) => item.origin !== "AutoGenerated");
    const paid = money(Math.abs(row.amount));
    const detailed = realItems.reduce((sum, item) => sum + (isAllocation(row) ? money(item.totalPrice) : item.totalPrice), 0);
    paidTotal += paid;
    coveredPaidTotal += Math.min(paid, Math.max(0, detailed));
    if (isAllocation(row)) {
      const allocated = realItems.reduce((sum, item) => sum + money(item.totalPrice), 0);
      for (const item of realItems) addSlice(item.category || "Unallocated", item.subcategory || "Unallocated", money(item.totalPrice), false);
      const remainder = money(Math.max(0, Math.abs(row.amount) - allocated));
      if (remainder > 0) {
        const source = bankClassification(row);
        addSlice(source.category, source.subcategory, remainder, true);
      }
      continue;
    }
    const generatedItems = row.items.filter((item) => item.origin === "AutoGenerated");
    const classificationOnly = realItems.length === 0 && generatedItems.length > 0;
    const items = realItems.length > 0 ? realItems : generatedItems;
    for (const item of items) {
      const category = item.category ?? "Other";
      const subcategory = item.subcategory ?? "Other";
      // Preserve legacy receipt rollups; actual Bill detail and signed Money Delta are distinct
      // from Bank/Manual allocation accounting above.
      const amount = classificationOnly ? (item === items[0] ? Math.abs(row.amount) : 0) : item.totalPrice;
      addSlice(category, subcategory, amount, item.origin === "AutoGenerated");
    }

    // Money Delta is a reconciled-row fact: the bank's amount against what the paper explained.
    if (row.dataOrigin === "Reconciled") {
      const delta = Math.abs(row.amount) - lineItemTotal(row);
      moneyDeltaTotal += delta;
      moneyDeltaRows += 1;
      moneyDeltaByTransaction.push({ id: row.id, description: row.description, amount: delta });
    }
  }

  return {
    categoryMatrix,
    categorySubcategoryMatrix,
    total,
    estimatedMatrix,
    coverage: paidTotal > 0 ? Math.min(100, coveredPaidTotal / paidTotal * 100) : 0,
    moneyDelta: { total: moneyDeltaTotal, rows: moneyDeltaRows, byTransaction: moneyDeltaByTransaction },
  };
}

/**
 * A category matrix as a flat, ordered list — the shape a report table reads left to right, newest
 * spend first. Rows whose total is zero are dropped, so an empty category never shows.
 */
export function categoryRows(
  matrix: CategoryMatrix,
): { category: string; amount: number }[] {
  return Object.entries(matrix)
    .map(([category, amount]) => ({ category, amount }))
    .filter((row) => row.amount !== 0)
    .sort((a, b) => b.amount - a.amount);
}
