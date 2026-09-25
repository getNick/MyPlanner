import type { BackendTransaction } from "../types/receiptTypes";
import {
  billsForReconciliation,
  isProvisionalBill,
  isReconciled,
  moneyDeltaOf,
  paidThroughAStatement,
  provisionalBillAgeDays,
  reconciliationLabel,
} from "./reconciliation";

function row(overrides: Partial<BackendTransaction>): BackendTransaction {
  return {
    id: "transaction",
    userId: "household",
    type: "Expense",
    paymentMethodId: null,
    toPaymentMethodId: null,
    timestamp: new Date().toISOString(),
    amount: 42,
    currency: "UAH",
    baseAmount: null,
    description: "Market",
    additionalNotes: null,
    balanceAfter: null,
    dataOrigin: "Receipt",
    moneyDelta: null,
    rawTransactionData: null,
    items: [],
    ...overrides,
  };
}

describe("reconciliation predicates", () => {
  it("recognizes a Reconciled transaction as both a Bill and statement-backed", () => {
    const reconciled = row({ dataOrigin: "Reconciled", paymentMethodId: "card" });

    expect(isReconciled(reconciled)).toBe(true);
    expect(isProvisionalBill(reconciled)).toBe(false);
    expect(paidThroughAStatement(reconciled)).toBe(true);
    expect(billsForReconciliation([reconciled])).toEqual([reconciled]);
    expect(reconciliationLabel(reconciled)).toBe("Reconciled");
  });

  it("recognizes an unmatched Receipt as a Provisional Bill, but not a statement row", () => {
    const provisional = row({ dataOrigin: "Receipt" });

    expect(isReconciled(provisional)).toBe(false);
    expect(isProvisionalBill(provisional)).toBe(true);
    expect(paidThroughAStatement(provisional)).toBe(false);
    expect(reconciliationLabel(provisional)).toBe("Provisional");
  });

  it("does not label ordinary Bank or Manual rows as Bills", () => {
    const bank = row({ dataOrigin: "Bank" });
    const manual = row({ dataOrigin: "Manual" });

    expect(billsForReconciliation([bank, manual])).toEqual([]);
    expect(reconciliationLabel(bank)).toBeNull();
    expect(reconciliationLabel(manual)).toBeNull();
    expect(paidThroughAStatement(bank)).toBe(true);
    expect(paidThroughAStatement(manual)).toBe(false);
  });

  it("keeps only Bill-origin and Reconciled rows without duplicating them", () => {
    const oldReceipt = row({ id: "old", timestamp: "2026-05-02T10:00:00Z" });
    const reconciled = row({ id: "merged", dataOrigin: "Reconciled", timestamp: "2026-05-04T10:00:00Z" });
    const bank = row({ id: "bank", dataOrigin: "Bank", timestamp: "2026-05-05T10:00:00Z" });

    expect(billsForReconciliation([oldReceipt, bank, reconciled]).map((item) => item.id)).toEqual([
      "old",
      "merged",
    ]);
  });
});

describe("Provisional Bill age", () => {
  it("uses whole elapsed days from the transaction timestamp", () => {
    const now = new Date("2026-09-24T12:00:00Z");
    const bill = row({ timestamp: "2026-09-22T11:59:59Z" });

    expect(provisionalBillAgeDays(bill, now)).toBe(2);
  });

  it("returns null when a Bill has no usable timestamp", () => {
    expect(provisionalBillAgeDays(row({ timestamp: null }), new Date())).toBeNull();
    expect(provisionalBillAgeDays(row({ timestamp: "not-a-date" }), new Date())).toBeNull();
  });
});

describe("moneyDeltaOf", () => {
  it("returns the backend-derived Money Delta without recalculating or mutating it", () => {
    const reconciled = row({ dataOrigin: "Reconciled", moneyDelta: -3.25 });

    expect(moneyDeltaOf(reconciled)).toBe(-3.25);
    expect(reconciled.moneyDelta).toBe(-3.25);
  });

  it("returns null when a row has no Money Delta", () => {
    expect(moneyDeltaOf(row({ dataOrigin: "Receipt", moneyDelta: null }))).toBeNull();
  });
});
