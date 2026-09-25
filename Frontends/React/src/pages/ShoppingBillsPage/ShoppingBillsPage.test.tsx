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
