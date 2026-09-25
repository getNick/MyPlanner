import {
  ReceiptData,
  BackendTransaction,
} from "../types/receiptTypes";
import FinanceService from "../services/FinanceService";
import type { SavedBill } from "../types/receiptTypes";

/**
 * Shared receipt-parsing + mapping seam.
 *
 * The live shopping-bills view routes a captured image through this single
 * helper so parsing and backend→UI mapping stay in exactly one place. That is
 * the only cross-feature code seam here; changing it must not break the view.
 */

/**
 * Map a persisted backend transaction (the shape returned by the receipts
 * `process` endpoint) into the frontend `ReceiptData` model used by the UI.
 *
 * It is pure so it can be unit-tested in isolation: given a parsed response,
 * produce the correct saved-bill shape.
 */
export function mapBackendToReceiptData(
  raw: BackendTransaction,
  mapCurrency: (currency: string) => string = (c) => c,
): ReceiptData {
  return {
    merchantName:
      raw.description !== "Receipt purchase" ? raw.description : undefined,
    timestamp: raw.timestamp ?? undefined,
    totalAmount: Number(raw.amount),
    currency: mapCurrency(raw.currency),
    // Receipts never carry a payment method.
    paymentMethod: undefined,
    additionalNotes: raw.additionalNotes ?? undefined,
    items: (raw.items || []).map((item) => ({
      name: item.name ?? "",
      fullName: item.fullName ?? "",
      quantity: Number(item.quantity),
      unitPrice: Number(item.pricePerUnit),
      totalPrice: Number(item.totalPrice),
      category: item.category ?? undefined,
      subcategory: item.subcategory ?? undefined,
    })),
  };
}

/**
 * Map a persisted backend transaction into the display model used by the live
 * shopping-bills list. Preserves the server-owned `id`s on each line item so
 * they can be deleted server-side later. Pure for testability.
 */
export function backendTransactionToSavedBill(
  raw: BackendTransaction,
  mapCurrency: (currency: string) => string = (c) => c,
): SavedBill {
  return {
    id: raw.id,
    merchantName:
      raw.description !== "Receipt purchase" ? raw.description : undefined,
    timestamp: raw.timestamp ?? undefined,
    totalAmount: Number(raw.amount),
    currency: mapCurrency(raw.currency),
    additionalNotes: raw.additionalNotes ?? undefined,
    createdAt: raw.createdAt ?? "",
    tags: [],
    dataOrigin: raw.dataOrigin,
    moneyDelta: raw.moneyDelta,
    items: (raw.items || []).map((item) => ({
      id: item.id ?? undefined,
      name: item.name ?? "",
      fullName: item.fullName ?? "",
      quantity: Number(item.quantity),
      unitPrice: Number(item.pricePerUnit),
      totalPrice: Number(item.totalPrice),
      category: item.category ?? undefined,
      subcategory: item.subcategory ?? undefined,
    })),
  };
}

/**
 * Upload a receipt image and return the editable OCR result without creating a
 * ledger row. The caller retains the file and corrected fields until confirm.
 */
export async function processReceiptWithDetails(
  file: File,
  getToken: () => Promise<string | null>,
): Promise<{ data: ReceiptData }> {
  const financeService = new FinanceService(getToken);
  const result = await financeService.previewReceipt(file);
  return {
    data: {
      merchantName: result.merchantName,
      timestamp: result.timestamp ?? undefined,
      totalAmount: Number(result.totalAmount ?? result.items.reduce((sum, item) => sum + item.totalPrice, 0)),
      currency: financeService.mapCurrency(result.currency || "UAH"),
      paymentMethod: undefined,
      additionalNotes: result.additionalNotes,
      items: result.items.map((item) => ({
        name: item.name ?? "", fullName: item.fullName ?? item.name ?? "",
        quantity: Number(item.quantity), unitPrice: Number(item.unitPrice),
        totalPrice: Number(item.totalPrice), category: item.category ?? undefined,
        subcategory: item.subcategory ?? undefined,
      })),
    },
  };
}
