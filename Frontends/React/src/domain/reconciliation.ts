import type { BackendTransaction } from "../types/receiptTypes";

const DAY_IN_MILLISECONDS = 24 * 60 * 60 * 1000;

/** A Bank Transaction is a row read from a bank statement, excluding Bills. */
export function isBankTransaction(row: Pick<BackendTransaction, "dataOrigin">): boolean {
  return row.dataOrigin === "Bank";
}

/** A Reconciled row is the single ledger row for a Bill matched to a Bank Transaction. */
export function isReconciled(row: Pick<BackendTransaction, "dataOrigin">): boolean {
  return row.dataOrigin === "Reconciled";
}

/** A Receipt-origin transaction has no Bank Transaction behind it yet. */
export function isProvisionalBill(row: Pick<BackendTransaction, "dataOrigin">): boolean {
  return row.dataOrigin === "Receipt";
}

/** Bank-origin and Reconciled rows both represent money paid through a statement. */
export function paidThroughAStatement(row: BackendTransaction): boolean {
  return isBankTransaction(row) || isReconciled(row);
}

/** The Bills list includes unmatched Bills and merged Bills, once each. */
export function billsForReconciliation(
  rows: BackendTransaction[],
): BackendTransaction[] {
  return rows.filter((row) => isProvisionalBill(row) || isReconciled(row));
}

/** State shown in the Bills list; ordinary Bank and Manual rows have no Bill state. */
export function reconciliationLabel(
  row: Pick<BackendTransaction, "dataOrigin">,
): "Reconciled" | "Provisional" | null {
  if (isReconciled(row)) return "Reconciled";
  if (isProvisionalBill(row)) return "Provisional";
  return null;
}

/**
 * Days since the Bill's transaction timestamp. The payload has no creation timestamp, so Timestamp
 * is the available age anchor. Invalid, missing, or future timestamps cannot produce a negative age.
 */
export function provisionalBillAgeDays(
  row: Pick<BackendTransaction, "dataOrigin"> & { timestamp?: string | null },
  now: Date = new Date(),
): number | null {
  if (!isProvisionalBill(row) || !row.timestamp) return null;
  const timestamp = new Date(row.timestamp).getTime();
  if (!Number.isFinite(timestamp)) return null;
  return Math.max(0, Math.floor((now.getTime() - timestamp) / DAY_IN_MILLISECONDS));
}

/** Money Delta is supplied by the backend's derived, non-persisted property. */
export function moneyDeltaOf(
  row: Pick<BackendTransaction, "moneyDelta">,
): number | null {
  return row.moneyDelta ?? null;
}
