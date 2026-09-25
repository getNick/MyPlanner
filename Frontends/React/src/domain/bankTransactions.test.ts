import {
  carriesSeparateBaseAmount,
  isBankTransaction,
  statementRowsFor,
} from "./bankTransactions";
import type { BackendTransaction } from "../types/receiptTypes";

/**
 * Which ledger rows are Bank Transactions, in what order, and when the card-currency figure is worth
 * printing. `/finance/bank` lists through these, so the page cannot quietly invent its own idea of "a bank
 * row" or of newest-first.
 */

function row(overrides: Partial<BackendTransaction>): BackendTransaction {
  return {
    id: "tx-1",
    userId: "u1",
    type: "Expense",
    paymentMethodId: "pm-mono",
    toPaymentMethodId: null,
    timestamp: "2026-07-01T09:09:25",
    amount: 59.99,
    currency: "UAH",
    baseAmount: 59.99,
    description: "Silpo",
    additionalNotes: null,
    balanceAfter: null,
    dataOrigin: "Bank",
    moneyDelta: null,
    rawTransactionData: null,
    items: [],
    ...overrides,
  };
}

describe("isBankTransaction", () => {
  it("accepts a row read from a statement", () => {
    expect(isBankTransaction(row({}))).toBe(true);
  });

  it("rejects a Bill and a hand-typed row", () => {
    expect(isBankTransaction(row({ dataOrigin: "Receipt" }))).toBe(false);
    expect(isBankTransaction(row({ dataOrigin: "Manual" }))).toBe(false);
  });
});

describe("statementRowsFor", () => {
  const salary = row({ id: "tx-new", timestamp: "2026-07-11T17:30:53", description: "Salary" });
  const silpo = row({ id: "tx-old", timestamp: "2026-07-01T09:09:25" });
  const savings = row({ id: "tx-other", paymentMethodId: "pm-save", description: "Mono save row" });
  const bill = row({ id: "tx-bill", dataOrigin: "Receipt", paymentMethodId: null, description: "Coffee House" });
  const reconciled = row({
    id: "tx-reconciled",
    dataOrigin: "Reconciled",
    timestamp: "2026-07-12T09:00:00",
    description: "Merged Silpo",
    moneyDelta: -1.5,
  });

  it("orders newest first", () => {
    expect(statementRowsFor([silpo, salary], "pm-mono").map((r) => r.id)).toEqual([
      "tx-new",
      "tx-old",
    ]);
  });

  it("keeps Bank and Reconciled rows for the chosen method, excluding Bills and other methods", () => {
    const listed = statementRowsFor([silpo, savings, bill, reconciled], "pm-mono");
    expect(listed.map((r) => r.id)).toEqual(["tx-reconciled", "tx-old"]);
  });

  it("lists nothing when no method is chosen", () => {
    expect(statementRowsFor([silpo], null)).toEqual([]);
  });

  it("puts a row with no date last rather than dropping it", () => {
    const undated = row({ id: "tx-undated", timestamp: null, createdAt: "2026-06-01T00:00:00Z" });
    expect(statementRowsFor([undated, silpo], "pm-mono").map((r) => r.id)).toEqual([
      "tx-old",
      "tx-undated",
    ]);
  });
});

describe("carriesSeparateBaseAmount", () => {
  it("is true when the card was charged a different figure — a foreign-currency purchase", () => {
    const aliexpress = row({ description: "AliExpress", currency: "EURO", amount: 13.37, baseAmount: 598.99 });
    expect(carriesSeparateBaseAmount(aliexpress)).toBe(true);
  });

  it("is false when the statement's card column repeats the amount", () => {
    expect(carriesSeparateBaseAmount(row({ amount: 59.99, baseAmount: 59.99 }))).toBe(false);
  });

  it("is false when there is no card-currency figure at all", () => {
    expect(carriesSeparateBaseAmount(row({ baseAmount: null }))).toBe(false);
  });
});
