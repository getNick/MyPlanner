// ── Payment Method (backend entity, see Backends/DotNet MyPlanner.Data.Entities.Finance) ──

export type BackendPaymentMethodType = "BankCard" | "Cash" | "SavingsAccount" | "Other";

export type BackendCurrency = "UAH" | "USD" | "EURO";

export interface BackendPaymentMethod {
  id: string;
  userId: string;
  name: string;
  type: BackendPaymentMethodType;
  currency: BackendCurrency;
  /** Bank whose statement format parses into this method (e.g. "Monobank"). Optional. */
  bankProvider: string | null;
}

export interface PaymentMethodCreateBody {
  name: string;
  type: BackendPaymentMethodType;
  currency: BackendCurrency;
  bankProvider?: string | null;
}

export interface PaymentMethodUpdateBody extends PaymentMethodCreateBody {
  id: string;
}

/**
 * Outcome of DELETE /finance/payment-methods/{id}.
 *
 * A refusal is a normal outcome, not an exception: a payment method with
 * transactions is never deleted underneath them. `transactionCount` is the
 * count the server reported, so the UI states the real number instead of
 * guessing at it. Thrown errors are reserved for transport/server faults.
 */
export type PaymentMethodDeletion =
  | { status: "deleted" }
  | { status: "not-found" }
  | {
      status: "in-use";
      /** How many transactions block the deletion; null when the server refused without saying. */
      transactionCount: number | null;
      /** Server-side wording, when the API supplied one. */
      explanation: string | null;
    };
