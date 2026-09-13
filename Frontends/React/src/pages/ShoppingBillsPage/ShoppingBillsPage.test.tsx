import React from "react";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import ShoppingBillsPage from "./ShoppingBillsPage";

// The view needs a token and a finance client; both are mocked so the test
// exercises the component's own delete/cleanup logic, not the network. The
// client is a real class (via the mock factory) whose instances share these
// module-level jest.fn.s, so `new FinanceService(...)` (as the component does)
// reliably exposes them for both setup and assertions.
const mockGetTransactions = jest.fn();
const mockGetReceiptCategories = jest.fn();
const mockGetTransactionItems = jest.fn();
const mockMapCurrency = jest.fn((c: string) => c);
const mockDeleteTransaction = jest.fn();
const mockUpdateTransaction = jest.fn();
const mockUpdateItem = jest.fn();
const mockCreateItem = jest.fn();
const mockDeleteItem = jest.fn();

// Return a STABLE getToken so the component's finance client (memoized on the
// token) doesn't churn and re-trigger the initial load on every render.
jest.mock("@clerk/clerk-react", () => {
  const stableToken = async () => "token";
  return { useAuth: () => ({ getToken: stableToken }) };
});

jest.mock("../../services/FinanceService", () => {
  class MockFinanceService {
    getTransactions = mockGetTransactions;
    getReceiptCategories = mockGetReceiptCategories;
    getTransactionItems = mockGetTransactionItems;
    mapCurrency = mockMapCurrency;
    deleteTransaction = mockDeleteTransaction;
    updateTransaction = mockUpdateTransaction;
    updateTransactionItem = mockUpdateItem;
    createTransactionItem = mockCreateItem;
    deleteTransactionItem = mockDeleteItem;
  }
  return { __esModule: true, default: MockFinanceService };
});

const SAMPLE_RECEIPT = {
  id: "tx-1",
  userId: "u1",
  type: "Expense",
  paymentMethodId: null,
  toPaymentMethodId: null,
  timestamp: "2024-05-01T10:30:00Z",
  amount: 42.5,
  currency: "USD",
  description: "Coffee House",
  additionalNotes: null,
  balanceAfter: null,
  dataOrigin: "Receipt",
  rawTransactionData: null,
  createdAt: "2024-05-01T10:30:00Z",
  items: [],
} as unknown as Record<string, unknown>;

describe("ShoppingBillsPage — reconciliation visibility", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    const daysAgo = (days: number) => new Date(Date.now() - days * 86_400_000).toISOString();
    mockGetTransactions.mockResolvedValue([
      { ...SAMPLE_RECEIPT, id: "tx-provisional", timestamp: daysAgo(8) },
      {
        ...SAMPLE_RECEIPT,
        id: "tx-reconciled",
        description: "Silpo",
        timestamp: daysAgo(2),
        paymentMethodId: "pm-mono",
        dataOrigin: "Reconciled",
        moneyDelta: 2.5,
      },
    ]);
    mockGetReceiptCategories.mockResolvedValue([]);
    mockGetTransactionItems.mockResolvedValue([]);
    mockDeleteTransaction.mockResolvedValue(true);
  });

  it("lists a Reconciled purchase once with its Money Delta and labels the unmatched Bill with age", async () => {
    render(<ShoppingBillsPage />);

    expect(await screen.findByText("Silpo")).toBeTruthy();
    expect(screen.getAllByText("Silpo")).toHaveLength(1);
    expect(screen.getByText("Reconciled")).toBeTruthy();
    expect(screen.getByText(/Money Delta.*2[.,]50/)).toBeTruthy();
    expect(screen.getByText("Provisional")).toBeTruthy();
    expect(screen.getByText(/No bank match · 8 days/)).toBeTruthy();
  });
});

describe("ShoppingBillsPage — complete Bill save", () => {
  it("saves the complete draft once and displays the surviving Reconciled Bill", async () => {
    jest.clearAllMocks();
    const item = {
      id: "item-1", transactionId: "tx-1", name: "Coffee", fullName: "Coffee",
      quantity: 1, pricePerUnit: 42.5, totalPrice: 42.5,
      category: null, subcategory: null, origin: "ReceiptParsed",
    };
    mockGetTransactions.mockResolvedValue([{ ...SAMPLE_RECEIPT, items: [item] }]);
    mockGetReceiptCategories.mockResolvedValue([]);
    mockGetTransactionItems.mockResolvedValue([item]);
    mockUpdateTransaction.mockResolvedValue({
      ...SAMPLE_RECEIPT, dataOrigin: "Reconciled", amount: 42.5, moneyDelta: 2.5,
      paymentMethodId: "pm-card", timestamp: "2024-05-01T10:45:00Z",
      items: [{ ...item, pricePerUnit: 40, totalPrice: 40, origin: "ManualInput" }],
    });

    render(<ShoppingBillsPage />);
    expect(await screen.findByText("Coffee House")).toBeTruthy();
    fireEvent.click(screen.getByLabelText("Expand report"));
    await screen.findByText("Coffee");
    await waitFor(() => expect(screen.queryByText("Loading line items...")).toBeNull());
    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Edit"));
    const price = await screen.findByLabelText("Edit item 1 price");
    fireEvent.change(price, { target: { value: "40" } });
    fireEvent.click(screen.getByText("Save"));

    await waitFor(() => expect(mockUpdateTransaction).toHaveBeenCalledWith(expect.objectContaining({
      id: "tx-1", amount: 40,
      items: [expect.objectContaining({ id: "item-1", pricePerUnit: 40, totalPrice: 40 })],
    })));
    expect(mockUpdateTransaction).toHaveBeenCalledTimes(1);
    expect(mockUpdateItem).not.toHaveBeenCalled();
    expect(mockCreateItem).not.toHaveBeenCalled();
    expect(mockDeleteItem).not.toHaveBeenCalled();
    expect(await screen.findByText("Reconciled")).toBeTruthy();
    expect(screen.getByText(/Money Delta.*2[.,]50/)).toBeTruthy();
  });

  it("keeps the complete draft editable when the atomic save fails", async () => {
    jest.clearAllMocks();
    mockGetTransactions.mockResolvedValue([SAMPLE_RECEIPT]);
    mockGetReceiptCategories.mockResolvedValue([]);
    mockUpdateTransaction.mockRejectedValue(new Error("Save failed"));
    render(<ShoppingBillsPage />);
    expect(await screen.findByText("Coffee House")).toBeTruthy();
    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Edit"));
    fireEvent.change(screen.getByDisplayValue("Coffee House"), { target: { value: "Corrected merchant" } });
    fireEvent.click(screen.getByText("Save"));

    await screen.findAllByText("Save failed");
    expect(screen.getByDisplayValue("Corrected merchant")).toBeTruthy();
    expect(screen.getByText("Save")).toBeTruthy();
    expect(mockUpdateTransaction).toHaveBeenCalledWith(expect.objectContaining({
      description: "Corrected merchant", amount: 0, items: [],
    }));
    expect(mockUpdateItem).not.toHaveBeenCalled();
    expect(mockCreateItem).not.toHaveBeenCalled();
    expect(mockDeleteItem).not.toHaveBeenCalled();
  });
});

describe("ShoppingBillsPage — remove transaction", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetTransactions.mockResolvedValue([SAMPLE_RECEIPT]);
    mockGetReceiptCategories.mockResolvedValue([]);
    mockGetTransactionItems.mockResolvedValue([]);
    mockDeleteTransaction.mockResolvedValue(true);
  });

  it("issues a transaction DELETE and drops the row when Remove is confirmed", async () => {
    window.confirm = jest.fn().mockReturnValue(true);

    render(<ShoppingBillsPage />);

    // The receipt row appears after the initial load.
    expect(await screen.findByText("Coffee House")).toBeTruthy();

    // Open the ⋮ menu and choose Remove.
    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Remove"));

    // The DELETE is issued with the receipt id (immediate, not staged).
    await waitFor(() => {
      expect(mockDeleteTransaction).toHaveBeenCalledWith("tx-1");
    });

    // The row is removed from the list — no ghost entry left behind, and the
    // processed-receipts header count drops to zero.
    await waitFor(() => {
      expect(screen.queryByText("Coffee House")).toBeNull();
    });
    await waitFor(() => {
      expect(screen.getByText("PROCESSED RECEIPTS (0)")).toBeTruthy();
    });
  });

  it("does not delete when the confirmation is dismissed", async () => {
    window.confirm = jest.fn().mockReturnValue(false);

    render(<ShoppingBillsPage />);

    expect(await screen.findByText("Coffee House")).toBeTruthy();

    fireEvent.click(screen.getByTitle("More options"));
    fireEvent.click(screen.getByText("Remove"));

    expect(mockDeleteTransaction).not.toHaveBeenCalled();
    expect(screen.queryByText("Coffee House")).not.toBeNull();
  });
});
