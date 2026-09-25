/**
 * Shapes the bank statement import answers with (FinanceController `banking-files/*`).
 * The summary is what one statement file says; the preview and the import echo the same
 * summary, so what the user confirmed is what was read.
 */

/** One statement row that could not be read — line number, what the bank printed, why it is not bookable. */
export interface BankStatementReviewRow {
  /** 1-based line of the file; the header is line 1. */
  rowNumber: number;
  rawValue: string | null;
  description: string | null;
  reason: string;
}

/** What a statement file says, before anything is written down. */
export interface BankStatementSummary {
  paymentMethodId: string;
  paymentMethodName: string;
  bankProvider: string;
  rowCount: number;
  firstTimestamp: string | null;
  lastTimestamp: string | null;
  needsReview: BankStatementReviewRow[];
}

/** The outcome of a confirmed import: what the file said, how much was new, how much was already there. */
export interface BankStatementImportResult {
  summary: BankStatementSummary;
  insertedRowCount: number;
  duplicateRowCount: number;
  // The stored rows are echoed here, but `/finance/bank` reads its list from GET /finance/transactions (the
  // same read as every other ledger view), so the payload rows stay unmodelled rather than becoming
  // a second source of truth.
  transactions: unknown[];
}
