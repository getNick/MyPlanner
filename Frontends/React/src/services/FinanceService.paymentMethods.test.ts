import FinanceService from "./FinanceService";
import type { BackendPaymentMethod } from "../types/paymentMethodTypes";

/**
 * HTTP-contract tests for the payment-method endpoints behind the bank page.
 * fetch is stubbed (no network), so these pin the exact URL/method/body each
 * call uses and, importantly, how a 409 "in use" refusal is surfaced: as data
 * with the server's transaction count, not as a thrown error.
 */

function makeService(): FinanceService {
  return new FinanceService(async () => "tok");
}

const CASH_WALLET: BackendPaymentMethod = {
  id: "pm-1",
  userId: "u1",
  name: "Cash Wallet",
  type: "Cash",
  currency: "UAH",
  bankProvider: null,
};

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function lastCall(fetchMock: jest.SpyInstance, index = 0): [string, RequestInit] {
  const call = fetchMock.mock.calls[index] as [string, RequestInit];
  return [call[0], call[1] ?? {}];
}

describe("FinanceService payment methods", () => {
  let fetchMock: jest.SpyInstance;

  beforeEach(() => {
    fetchMock = jest.spyOn(global, "fetch").mockResolvedValue(json([]));
  });

  afterEach(() => {
    fetchMock.mockRestore();
  });

  describe("getPaymentMethods", () => {
    it("GETs finance/payment-methods with auth and returns the list", async () => {
      fetchMock.mockResolvedValue(json([CASH_WALLET]));

      await expect(makeService().getPaymentMethods()).resolves.toEqual([CASH_WALLET]);

      const [url, init] = lastCall(fetchMock);
      expect(url).toContain("/finance/payment-methods");
      expect(init.method).toBe("GET");
      expect((init.headers as Record<string, string>).Authorization).toBe("Bearer tok");
    });

    it("throws when the server fails, so an empty ledger is not implied", async () => {
      fetchMock.mockResolvedValue(new Response(null, { status: 500, statusText: "Server Error" }));
      await expect(makeService().getPaymentMethods()).rejects.toThrow(
        /Failed to fetch payment methods/
      );
    });
  });

  describe("createPaymentMethod", () => {
    it("POSTs the full body including bankProvider", async () => {
      fetchMock.mockResolvedValue(new Response("\"pm-2\"", { status: 201 }));

      await expect(
        makeService().createPaymentMethod({
          name: "Monobank black",
          type: "BankCard",
          currency: "UAH",
          bankProvider: "Monobank",
        })
      ).resolves.toBeUndefined();

      const [url, init] = lastCall(fetchMock);
      expect(url).toContain("/finance/payment-methods");
      expect(init.method).toBe("POST");
      expect(JSON.parse(init.body as string)).toEqual({
        name: "Monobank black",
        type: "BankCard",
        currency: "UAH",
        bankProvider: "Monobank",
      });
    });

    it("throws when the server rejects the payload", async () => {
      fetchMock.mockResolvedValue(new Response(null, { status: 400, statusText: "Bad Request" }));
      await expect(
        makeService().createPaymentMethod({ name: "", type: "BankCard", currency: "UAH" })
      ).rejects.toThrow(/Failed to create payment method/);
    });
  });

  describe("updatePaymentMethod", () => {
    it("PUTs finance/payment-methods/{id} with the id in the body", async () => {
      fetchMock.mockResolvedValue(new Response(null, { status: 200 }));

      const ok = await makeService().updatePaymentMethod({
        id: "pm-2",
        name: "Renamed",
        type: "SavingsAccount",
        currency: "USD",
        bankProvider: null,
      });

      expect(ok).toBe(true);
      const [url, init] = lastCall(fetchMock);
      expect(url).toContain("/finance/payment-methods/pm-2");
      expect(init.method).toBe("PUT");
      expect(JSON.parse(init.body as string)).toEqual({
        id: "pm-2",
        name: "Renamed",
        type: "SavingsAccount",
        currency: "USD",
        bankProvider: null,
      });
    });

    it("returns false when the method is not there (404)", async () => {
      fetchMock.mockResolvedValue(new Response(null, { status: 404 }));
      await expect(
        makeService().updatePaymentMethod({ id: "gone", name: "x", type: "Cash", currency: "UAH" })
      ).resolves.toBe(false);
    });
  });

  describe("deletePaymentMethod", () => {
    it("reports deleted on 204", async () => {
      fetchMock.mockResolvedValue(new Response(null, { status: 204 }));
      await expect(makeService().deletePaymentMethod("pm-2")).resolves.toEqual({ status: "deleted" });

      const [url, init] = lastCall(fetchMock);
      expect(url).toContain("/finance/payment-methods/pm-2");
      expect(init.method).toBe("DELETE");
    });

    it("reports not-found on 404", async () => {
      fetchMock.mockResolvedValue(new Response(null, { status: 404 }));
      await expect(makeService().deletePaymentMethod("pm-2")).resolves.toEqual({
        status: "not-found",
      });
    });

    it("surfaces a refusal with the server's transaction count (409)", async () => {
      fetchMock.mockResolvedValue(
        json(
          { error: "3 transactions recorded on this payment method.", transactionCount: 3 },
          409
        )
      );

      await expect(makeService().deletePaymentMethod("pm-2")).resolves.toEqual({
        status: "in-use",
        transactionCount: 3,
        explanation: "3 transactions recorded on this payment method.",
      });
    });

    it("defaults the count when the body is missing, but still refuses", async () => {
      fetchMock.mockResolvedValue(new Response(null, { status: 409 }));

      const result = await makeService().deletePaymentMethod("pm-2");
      expect(result.status).toBe("in-use");
      if (result.status === "in-use") {
        // No number invented: the refusal stands, the count stays unknown.
        expect(result.transactionCount).toBeNull();
        expect(result.explanation).toBeNull();
      }
    });

    it("throws on a server fault rather than pretending the row is safe", async () => {
      fetchMock.mockResolvedValue(new Response(null, { status: 500, statusText: "Server Error" }));
      await expect(makeService().deletePaymentMethod("pm-2")).rejects.toThrow(
        /Failed to delete payment method/
      );
    });
  });
});
