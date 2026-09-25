import {
  BankStatementImportResult,
  BankStatementSummary,
} from "../types/bankImportTypes";
import {
  BackendTransaction,
  BackendTransactionItem,
  ReceiptData,
  TransactionCreateBody,
  TransactionItemCreateBody,
  TransactionUpdateBody,
  TransactionItemUpdateBody,
} from "../types/receiptTypes";
import type {
  BackendPaymentMethod,
  PaymentMethodCreateBody,
  PaymentMethodDeletion,
  PaymentMethodUpdateBody,
} from "../types/paymentMethodTypes";

export default class FinanceService {
  private _baseUrl: string = process.env.REACT_APP_API_URL ?? "http://localhost:5206/api/";
  private getToken: () => Promise<string | null>;

  constructor(getToken: () => Promise<string | null>) {
    this.getToken = getToken;
  }

  // ── Receipt Categories (public, no auth) ────────────────────────────

  public async getReceiptCategories(): Promise<
    { name: string; subcategories: string[] }[]
  > {
    try {
      const response = await fetch(`${this._baseUrl}finance/receipts/categories`);
      if (!response.ok) return [];
      return await response.json();
    } catch (error) {
      console.error("Failed to fetch receipt categories:", error);
      return [];
    }
  }

  // ── Payment Methods ──────────────────────────────────────────────

  /**
   * Unlike the transaction list, a failed read throws instead of returning [].
   * "No payment methods" and "could not read payment methods" look identical on
   * screen otherwise, and an empty-looking ledger is the wrong thing to imply.
   */
  public async getPaymentMethods(): Promise<BackendPaymentMethod[]> {
    try {
      const token = await this.getToken();
      const response = await fetch(`${this._baseUrl}finance/payment-methods`, {
        method: "GET",
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        throw new Error(
          `Failed to fetch payment methods: ${response.status} ${response.statusText}`
        );
      }

      return await response.json();
    } catch (error) {
      console.error("Error fetching payment methods:", error);
      throw error;
    }
  }

  /** Creates the method. Resolves when the server accepted it; throws when it did not. */
  public async createPaymentMethod(body: PaymentMethodCreateBody): Promise<void> {
    try {
      const token = await this.getToken();
      const response = await fetch(`${this._baseUrl}finance/payment-methods`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify(body),
      });

      if (!response.ok) {
        const problemDetails = await response.json().catch(() => null);
        throw new Error(
          `Failed to create payment method: ${response.status} ${response.statusText}` +
            (problemDetails?.title ? ` — ${problemDetails.title}` : "")
        );
      }
    } catch (error) {
      console.error("Error creating payment method:", error);
      throw error;
    }
  }

  /** Returns false when the server reports the method is gone (404). */
  public async updatePaymentMethod(body: PaymentMethodUpdateBody): Promise<boolean> {
    try {
      const token = await this.getToken();
      const response = await fetch(
        `${this._baseUrl}finance/payment-methods/${body.id}`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify(body),
        }
      );

      if (response.status === 404) return false;
      if (!response.ok) {
        const problemDetails = await response.json().catch(() => null);
        throw new Error(
          `Failed to update payment method: ${response.status} ${response.statusText}` +
            (problemDetails?.title ? ` — ${problemDetails.title}` : "")
        );
      }

      return true;
    } catch (error) {
      console.error(`Error updating payment method ${body.id}:`, error);
      throw error;
    }
  }

  /**
   * Delete a payment method. A refusal is returned as data (`in-use` with the
   * server's transaction count), never thrown: money in use is not removed, and
   * the caller has to explain why. Only transport/server faults throw.
   */
  public async deletePaymentMethod(id: string): Promise<PaymentMethodDeletion> {
    try {
      const token = await this.getToken();
      const response = await fetch(`${this._baseUrl}finance/payment-methods/${id}`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${token}` },
      });

      if (response.status === 204) return { status: "deleted" };
      if (response.status === 404) return { status: "not-found" };

      if (response.status === 409) {
        const payload = await response.json().catch(() => null);
        return {
          status: "in-use",
          transactionCount:
            typeof payload?.transactionCount === "number" ? payload.transactionCount : null,
          explanation: typeof payload?.error === "string" ? payload.error : null,
        };
      }

      throw new Error(
        `Failed to delete payment method: ${response.status} ${response.statusText}`
      );
    } catch (error) {
      console.error(`Error deleting payment method ${id}:`, error);
      throw error;
    }
  }

  // ── Transactions ────────────────────────────────────────────────────

  /**
   * The household's whole ledger.
   *
   * A failed read throws rather than answering with `[]`: `/finance/bank` has to be able to say "could not
   * read the ledger" instead of implying an import inserted nothing, and every caller already
   * handles the exception. (`getPaymentMethods` set this precedent for the same reason.)
   */
  public async getTransactions(range?: { from: string | null; to: string | null }): Promise<BackendTransaction[]> {
    try {
      const token = await this.getToken();
      const query = new URLSearchParams();
      if (range?.from) query.set("startDate", `${range.from}T00:00:00`);
      if (range?.to) query.set("endDate", `${range.to}T23:59:59.999`);
      const queryString = query.toString();
      const suffix = queryString ? `?${queryString}` : "";
      const response = await fetch(`${this._baseUrl}finance/transactions${suffix}`, {
        method: "GET",
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        throw new Error(
          `Failed to fetch transactions: ${response.status} ${response.statusText}`
        );
      }

      return await response.json();
    } catch (error) {
      console.error("Error fetching transactions:", error);
      throw error;
    }
  }

  public async getTransaction(id: string): Promise<BackendTransaction | null> {
    try {
      const token = await this.getToken();
      const response = await fetch(`${this._baseUrl}finance/transactions/${id}`, {
        method: "GET",
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) return null;
      return await response.json();
    } catch (error) {
      console.error(`Error fetching transaction ${id}:`, error);
      return null;
    }
  }

  public async createTransaction(
    body: TransactionCreateBody
  ): Promise<string | null> {
    try {
      const token = await this.getToken();
      const response = await fetch(`${this._baseUrl}finance/transactions`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify(body),
      });

      if (!response.ok) {
        const problemDetails = await response.json().catch(() => null);
        throw new Error(
          `Failed to create transaction: ${response.status} ${response.statusText}` +
            (problemDetails?.title ? ` — ${problemDetails.title}` : "")
        );
      }

      // Backend returns CreatedAtAction → redirect, but we need the ID.
      // The response location header contains the URL with the new ID.
      const location = response.headers.get("Location");
      if (location) {
        return location.split("/").pop() ?? null;
      }

      // Fallback: try to read from body
      const data = await response.json();
      return typeof data === "string" ? data : data.id ?? null;
    } catch (error) {
      console.error("Error creating transaction:", error);
      throw error;
    }
  }

  public async deleteTransaction(id: string): Promise<boolean> {
    try {
      const token = await this.getToken();
      const response = await fetch(`${this._baseUrl}finance/transactions/${id}`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        throw new Error(
          `Failed to delete transaction: ${response.status} ${response.statusText}`
        );
      }

      return true;
    } catch (error) {
      console.error(`Error deleting transaction ${id}:`, error);
      throw error;
    }
  }

  // ── Transaction Items ───────────────────────────────────────────────

  /**
   * Delete a single line item from a stored transaction.
   * Backend returns 204 NoContent on success, 404 when the item is not found.
   */
  public async deleteTransactionItem(itemId: string): Promise<boolean> {
    try {
      const token = await this.getToken();
      const response = await fetch(
        `${this._baseUrl}finance/transactions/items/${itemId}`,
        {
          method: "DELETE",
          headers: { Authorization: `Bearer ${token}` },
        }
      );

      // 204 NoContent => success; 404 => item no longer exists (treat as gone).
      if (response.ok || response.status === 404) return true;

      throw new Error(
        `Failed to delete transaction item: ${response.status} ${response.statusText}`
      );
    } catch (error) {
      console.error(`Error deleting transaction item ${itemId}:`, error);
      throw error;
    }
  }

  public async getTransactionItems(
    txId: string
  ): Promise<BackendTransactionItem[]> {
    try {
      const token = await this.getToken();
      const response = await fetch(
        `${this._baseUrl}finance/transactions/${txId}/items`,
        {
          method: "GET",
          headers: { Authorization: `Bearer ${token}` },
        }
      );

      if (!response.ok) return [];
      return await response.json();
    } catch (error) {
      console.error(`Error fetching items for tx ${txId}:`, error);
      return [];
    }
  }

  public async createTransactionItem(
    txId: string,
    body: TransactionItemCreateBody
  ): Promise<string | null> {
    try {
      const token = await this.getToken();
      const response = await fetch(
        `${this._baseUrl}finance/transactions/${txId}/items`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify(body),
        }
      );

      if (!response.ok) {
        const problemDetails = await response.json().catch(() => null);
        throw new Error(
          `Failed to create transaction item: ${response.status} ${response.statusText}` +
            (problemDetails?.title ? ` — ${problemDetails.title}` : "")
        );
      }

      const location = response.headers.get("Location");
      if (location) {
        return location.split("/").pop() ?? null;
      }

      const data = await response.json();
      return typeof data === "string" ? data : data.id ?? null;
    } catch (error) {
      console.error(`Error creating item for tx ${txId}:`, error);
      throw error;
    }
  }

  // ── Updates (save-on-press commit path) ───────────────────────────

  /**
   * Persist header fields of a transaction via PUT /finance/transactions/{id}.
   * Returns true on success, false when the server reports NotFound (404).
   */
  public async updateTransaction(
    body: TransactionUpdateBody,
  ): Promise<boolean> {
    try {
      const token = await this.getToken();
      const response = await fetch(
        `${this._baseUrl}finance/transactions/${body.id}`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify(body),
        },
      );

      if (response.status === 404) return false;
      if (!response.ok) {
        const problemDetails = await response.json().catch(() => null);
        throw new Error(
          `Failed to update transaction: ${response.status} ${response.statusText}` +
            (problemDetails?.title ? ` — ${problemDetails.title}` : ""),
        );
      }

      return true;
    } catch (error) {
      console.error(`Error updating transaction ${body.id}:`, error);
      throw error;
    }
  }

  /**
   * Persist a single line item via PUT /finance/transactions/items/{itemId}.
   */
  public async updateTransactionItem(
    itemId: string,
    body: TransactionItemUpdateBody,
  ): Promise<boolean> {
    try {
      const token = await this.getToken();
      const response = await fetch(
        `${this._baseUrl}finance/transactions/items/${itemId}`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify(body),
        },
      );

      if (response.status === 404) return false;
      if (!response.ok) {
        const problemDetails = await response.json().catch(() => null);
        throw new Error(
          `Failed to update transaction item: ${response.status} ${response.statusText}` +
            (problemDetails?.title ? ` — ${problemDetails.title}` : ""),
        );
      }

      return true;
    } catch (error) {
      console.error(`Error updating item ${itemId}:`, error);
      throw error;
    }
  }

  // ── Receipt Processing ─────────────────────────────────────────────

  public async previewReceipt(file: File): Promise<ReceiptData> {
    const formData = new FormData();
    formData.append("file", file);
    return this.postReceipt("receipts/preview", formData);
  }

  public async confirmReceipt(file: File, receipt: ReceiptData, paymentMethodId?: string): Promise<BackendTransaction> {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("receipt", JSON.stringify(receipt));
    if (paymentMethodId) formData.append("paymentMethodId", paymentMethodId);
    return this.postReceipt("receipts/confirm", formData);
  }

  private async postReceipt<T>(endpoint: string, formData: FormData): Promise<T> {
    try {
      const token = await this.getToken();
      const response = await fetch(`${this._baseUrl}finance/${endpoint}`, {
        method: "POST",
        headers: { Authorization: `Bearer ${token}` },
        body: formData,
      });
      if (!response.ok) {
        const errPayload = await response.json().catch(() => ({}));
        throw new Error(errPayload.error || `HTTP error ${response.status} ${response.statusText}`);
      }
      return await response.json();
    } catch (error) {
      console.error(`Error in ${endpoint}:`, error);
      throw error;
    }
  }

  // ── Bank Statement Import ──────────────────────────────────────────

  /**
   * Reads a statement for the chosen payment method and says what it holds — row count, date
   * span, rows needing review — without storing anything. The confirmation step; the caller keeps
   * the File between the two calls.
   */
  public async previewBankingFile(
    paymentMethodId: string,
    file: File
  ): Promise<BankStatementSummary> {
    return this.postBankingStatement("banking-files/preview", paymentMethodId, file);
  }

  /** Inserts a confirmed statement. Refusals mirror the preview's and throw with the server's reason. */
  public async importBankingFile(
    paymentMethodId: string,
    file: File
  ): Promise<BankStatementImportResult> {
    return this.postBankingStatement("banking-files", paymentMethodId, file);
  }

  private async postBankingStatement(
    endpoint: string,
    paymentMethodId: string,
    file: File
  ): Promise<any> {
    try {
      const token = await this.getToken();
      const formData = new FormData();
      formData.append("paymentMethodId", paymentMethodId);
      formData.append("file", file);

      const response = await fetch(`${this._baseUrl}finance/${endpoint}`, {
        method: "POST",
        headers: { Authorization: `Bearer ${token}` },
        body: formData,
      });

      if (!response.ok) {
        // The refusal reason is the payload's whole point — "no bank set", "not a CSV statement" —
        // so it is rethrown as the message, not swallowed into a status code.
        const errPayload = await response.json().catch(() => ({}));
        throw new Error(
          errPayload.error || `HTTP error ${response.status} ${response.statusText}`
        );
      }

      return await response.json();
    } catch (error) {
      console.error(`Error in ${endpoint}:`, error);
      throw error;
    }
  }

  // ── Helpers ─────────────────────────────────────────────────────────

  /**
   * Map a backend Currency enum value to a frontend-friendly string.
   * EURO → "EUR" (ISO 4217), UAH/USD stay as-is.
   */
  public mapCurrency(currency: string): string {
    switch (currency) {
      case "EURO":
        return "EUR";
      case "UAH":
      case "USD":
        return currency;
      default:
        return "UAH"; // fallback
    }
  }

  /**
   * Calculate the total amount from a transaction's items.
   */
  public calculateTotalFromItems(items: BackendTransactionItem[]): number {
    return items.reduce((sum, item) => sum + item.totalPrice, 0);
  }
}
