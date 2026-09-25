import type { BackendTransaction } from "../types/receiptTypes";
import { paidThroughAStatement } from "./reconciliation";
export { isBankTransaction } from "./reconciliation";

/**
 * Which ledger rows a bank statement is answerable for, and in what order. `/finance/bank` lists through
 * these, so the page cannot invent its own idea of "a bank row" — the term comes from `CONTEXT.md`
 * (*Bank Transaction*: a Transaction whose `dataOrigin` is `Bank`).
 */

/**
 * Newest-first key. A statement always carries a timestamp (a row without a readable date is held
 * back by the import), so an undated bank row means a hand-made or legacy row: it is listed last,
 * never dropped, because a row the page hides is a row the user cannot check.
 */
function recencyKey(row: BackendTransaction): string {
  return row.timestamp ?? row.createdAt ?? "";
}

export function compareNewestFirst(a: BackendTransaction, b: BackendTransaction): number {
  return recencyKey(b).localeCompare(recencyKey(a));
}

/**
 * The statement rows of one Payment Method, newest first. Choosing an account to import into and
 * then listing a different account's rows would be worse than listing nothing, so the two always
 * name the same method.
 */
export function statementRowsFor(
  rows: BackendTransaction[],
  paymentMethodId: string | null
): BackendTransaction[] {
  if (!paymentMethodId) return [];

  return rows
    .filter((row) => paidThroughAStatement(row) && row.paymentMethodId === paymentMethodId)
    .sort(compareNewestFirst);
}

/**
 * Whether the row was paid in a currency other than the account's, which is exactly when the card's
 * own figure (`BaseAmount`, the statement's `Сума в валюті картки (UAH)`) says something the
 * transaction amount does not. On a domestic purchase the two columns are the same number, and
 * printing it twice is noise.
 */
export function carriesSeparateBaseAmount(row: BackendTransaction): boolean {
  return row.baseAmount != null && Number(row.baseAmount) !== Number(row.amount);
}
