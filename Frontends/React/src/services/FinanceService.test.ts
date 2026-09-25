import FinanceService from "./FinanceService";

/**
 * Tests for the real server-side delete path: `deleteTransactionItem`.
 * We stub global.fetch (no network) to assert the exact HTTP contract used by
 * the live shopping-bills view.
 */

function makeService(): FinanceService {
  return new FinanceService(async () => "tok");
}

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
