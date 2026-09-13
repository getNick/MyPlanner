import FinanceService from "./FinanceService";

/**
 * Tests for the real server-side delete path: `deleteTransactionItem`.
 * We stub global.fetch (no network) to assert the exact HTTP contract used by
 * the live shopping-bills view.
 */

function makeService(): FinanceService {
  return new FinanceService(async () => "tok");
}

describe("FinanceService.getTransactions date range", () => {
  let fetchMock: jest.SpyInstance;
  beforeEach(() => { fetchMock = jest.spyOn(global, "fetch").mockResolvedValue(new Response("[]", { status: 200 })); });
  afterEach(() => fetchMock.mockRestore());

  it("sends inclusive date bounds as transaction endpoint query parameters", async () => {
    await makeService().getTransactions({ from: "2026-09-01", to: "2026-09-30" });
    const url = new URL(fetchMock.mock.calls[0][0] as string);
    expect(url.pathname).toContain("finance/transactions");
    expect(url.searchParams.get("startDate")).toBe("2026-09-01T00:00:00");
    expect(url.searchParams.get("endDate")).toBe("2026-09-30T23:59:59.999");
  });
});

describe("FinanceService.createTransactionItem", () => {
  it("sends the classification origin with the categorized TransactionItem", async () => {
    const fetchMock = jest.spyOn(global, "fetch").mockResolvedValue(
      new Response(null, { status: 201, headers: { Location: "http://localhost/transactions/items/item-1" } }),
    );
    try {
      await makeService().createTransactionItem("tx-manual", {
        name: "Cash groceries",
        fullName: "Cash groceries",
        category: "Groceries",
        subcategory: "Pantry",
        quantity: 1,
        pricePerUnit: 42.5,
        totalPrice: 42.5,
        origin: "ManualInput",
      });

      const [, init] = fetchMock.mock.calls[0];
      expect((init as RequestInit).method).toBe("POST");
      expect(JSON.parse((init as RequestInit).body as string)).toMatchObject({
        category: "Groceries",
        subcategory: "Pantry",
        origin: "ManualInput",
        totalPrice: 42.5,
      });
    } finally {
      fetchMock.mockRestore();
    }
  });
});

describe("FinanceService.updateTransaction complete Bill contract", () => {
  const draft = {
    id: "tx-bill", type: "Expense" as const, paymentMethodId: null, toPaymentMethodId: null,
    timestamp: "2026-09-01T12:00:00", amount: 20, currency: "UAH", description: "Merchant",
    additionalNotes: null, balanceAfter: null, dataOrigin: "Receipt" as const,
    items: [{ name: "Bread", fullName: "Bread", category: null, subcategory: null,
      quantity: 1, pricePerUnit: 20, totalPrice: 20 }],
  };

  it("returns the surviving row from the existing PUT, not a success boolean", async () => {
    const surviving = { ...draft, dataOrigin: "Reconciled", moneyDelta: 0, paymentMethodId: "pm-card" };
    const fetchMock = jest.spyOn(global, "fetch").mockResolvedValue(
      new Response(JSON.stringify(surviving), { status: 200 }),
    );
    try {
      await expect(makeService().updateTransaction(draft)).resolves.toEqual(surviving);
      const [url, init] = fetchMock.mock.calls[0];
      expect(url).toContain("/finance/transactions/tx-bill");
      expect((init as RequestInit).method).toBe("PUT");
      expect(JSON.parse((init as RequestInit).body as string)).toEqual(draft);
    } finally { fetchMock.mockRestore(); }
  });

  it("returns null when the Bill no longer exists", async () => {
    const fetchMock = jest.spyOn(global, "fetch").mockResolvedValue(new Response(null, { status: 404 }));
    try { await expect(makeService().updateTransaction(draft)).resolves.toBeNull(); }
    finally { fetchMock.mockRestore(); }
  });

  it("surfaces the Bill validation error without treating the draft as saved", async () => {
    const fetchMock = jest.spyOn(global, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ error: "Line Item ID must belong to this Bill." }), { status: 400 }),
    );
    try {
      await expect(makeService().updateTransaction(draft)).rejects.toThrow("Line Item ID must belong to this Bill.");
    } finally { fetchMock.mockRestore(); }
  });
});

describe("FinanceService.deleteTransactionItem", () => {
  let fetchMock: jest.SpyInstance;

  beforeEach(() => {
    fetchMock = jest.spyOn(global, "fetch").mockResolvedValue(new Response(null, { status: 204 }));
  });

  afterEach(() => {
    fetchMock.mockRestore();
  });

  it("issues DELETE to finance/transactions/items/{id} with auth", async () => {
    await makeService().deleteTransactionItem("item-1");

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url as string).toContain("/finance/transactions/items/item-1");
    expect((init as RequestInit).method).toBe("DELETE");
    const headers = (init as RequestInit).headers as Record<string, string>;
    expect(headers.Authorization).toBe("Bearer tok");
  });

  it("returns true on success (204)", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }));
    await expect(makeService().deleteTransactionItem("item-1")).resolves.toBe(true);
  });

  it("treats a missing item (404) as gone", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 404 }));
    await expect(makeService().deleteTransactionItem("item-1")).resolves.toBe(true);
  });

  it("throws on server failure", async () => {
    fetchMock.mockResolvedValue(
      new Response("boom", { status: 500, statusText: "Server Error" }),
    );
    await expect(makeService().deleteTransactionItem("item-1")).rejects.toThrow(
      /Failed to delete transaction item/,
    );
  });
});
