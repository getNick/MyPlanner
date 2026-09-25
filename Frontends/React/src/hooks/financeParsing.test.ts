import {
  mapBackendToReceiptData,
  backendTransactionToSavedBill,
} from "./useProcessReceipt";
import type { BackendTransaction } from "../types/receiptTypes";

/**
 * Unit tests for the shared parsing/mapping seam. These lock in the exact
 * input → saved-bill shape the live shopping-bills view depends on, so any
 * regression here fails fast before it can diverge consumers.
 */

const sample: BackendTransaction = {
  id: "txn-1",
  userId: "user-1",
  type: "Expense",
  paymentMethodId: null,
  toPaymentMethodId: null,
  timestamp: "2024-05-01T10:30:00Z",
  amount: 42.5,
  currency: "USD",
  description: "Coffee House",
  additionalNotes: "Lunch with team",
  baseAmount: null,
  balanceAfter: null,
  dataOrigin: "Receipt",
  rawTransactionData: null,
  createdAt: "2024-05-01T11:00:00Z",
  items: [
    {
      id: "item-a",
      transactionId: "txn-1",
      name: "Coffee",
      fullName: "Large Coffee",
      category: "Food",
      subcategory: "Beverages",
      quantity: 2,
      pricePerUnit: 3.5,
      totalPrice: 7,
      origin: "ReceiptParsed",
    },
    {
      id: "item-b",
      transactionId: "txn-1",
      name: "Sandwich",
      fullName: "Club Sandwich",
      category: null,
      subcategory: null,
      quantity: 1,
      pricePerUnit: 35.5,
      totalPrice: 35.5,
      origin: "ReceiptParsed",
    },
  ],
};

describe("mapBackendToReceiptData", () => {
  it("maps header fields from a BackendTransaction into ReceiptData", () => {
    const result = mapBackendToReceiptData(sample);

    expect(result.merchantName).toBe("Coffee House");
    expect(result.timestamp).toBe("2024-05-01T10:30:00Z");
    expect(result.totalAmount).toBe(42.5);
    expect(result.paymentMethod).toBeUndefined();
    expect(result.additionalNotes).toBe("Lunch with team");
  });

  it("treats a default receipt description as no merchant name", () => {
    const result = mapBackendToReceiptData({ ...sample, description: "Receipt purchase" });
    expect(result.merchantName).toBeUndefined();
  });

  it("maps line items with numeric coercion and category fields", () => {
    const result = mapBackendToReceiptData(sample);

    expect(result.items).toHaveLength(2);
    expect(result.items[0]).toMatchObject({
      name: "Coffee",
      fullName: "Large Coffee",
      quantity: 2,
      unitPrice: 3.5,
      totalPrice: 7,
      category: "Food",
      subcategory: "Beverages",
    });
    // Items without a category fall back to undefined (not empty string).
    expect(result.items[1].category).toBeUndefined();
  });

  it("applies the provided currency mapper", () => {
    const result = mapBackendToReceiptData(sample, (c) => `${c}-MAPPED`);
    expect(result.currency).toBe("USD-MAPPED");
  });
});

describe("backendTransactionToSavedBill", () => {
  it("produces a SavedBill preserving server-owned ids and identity fields", () => {
    const result = backendTransactionToSavedBill(sample);

    expect(result.id).toBe("txn-1");
    expect(result.createdAt).toBe("2024-05-01T11:00:00Z");
    expect(result.tags).toEqual([]);
    expect(result.totalAmount).toBe(42.5);

    // Line-item ids are carried through so they can be deleted later.
    expect(result.items[0].id).toBe("item-a");
    expect(result.items[1].id).toBe("item-b");
  });

  it("maps currency using the mapper", () => {
    const result = backendTransactionToSavedBill(sample, (c) => c);
    expect(result.currency).toBe("USD");
  });
});
