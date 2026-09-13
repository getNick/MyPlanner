import React, { useCallback, useEffect, useMemo, useState } from "react";
import UploadZone from "../../components/UploadZone/UploadZone";
import BillReportPanel from "../../components/BillReportPanel/BillReportPanel";
import TransactionList from "../../components/TransactionList/TransactionList";
import FinanceService from "../../services/FinanceService";
import { backendTransactionToSavedBill, processReceiptWithDetails } from "../../hooks/useProcessReceipt";
import type {
  BackendTransaction,
  Category,
  ReceiptData,
  SavedBill,
} from "../../types/receiptTypes";
import {
  billsForReconciliation,
  moneyDeltaOf,
  provisionalBillAgeDays,
  reconciliationLabel,
} from "../../domain/reconciliation";
import { Loader2 } from "lucide-react";
import { useAuth } from "@clerk/clerk-react";

/**
 * Live, data-driven shopping-bills view.
 *
 * The visual foundation is the original light showcase design (white/slate/
 * zinc-900 cards on a `bg-slate-50` page with monospace type). All of its
 * "invoice-fiction" columns (Invoice #, OCR Confidence, Tax Status) are dropped
 * because no backend field backs them — the table now renders only real data:
 * Vendor / Date / Category / Amount. Unexpanded rows keep that exact flat look;
 * clicking a row expands it inline to reveal the structured BillReportPanel.
 * Edits are staged inside each self-contained BillReportPanel while editing
 * and committed once through PUT /transactions/{id}, including the complete Line
 * Item collection. Matching happens after the complete edit, in the same commit.
 * No individual item write is fired: the panel owns staging.
 */

// Newest-first comparison: createdAt (ISO) wins, else timestamp fallback.
function compareNewestFirst(a: SavedBill, b: SavedBill): number {
  const ta = a.createdAt || a.timestamp || "";
  const tb = b.createdAt || b.timestamp || "";
  return tb.localeCompare(ta);
}

function reconciliationBadge(bill: SavedBill): React.ReactNode {
  const label = reconciliationLabel(bill);
  if (!label) return null;

  const ageDays = provisionalBillAgeDays(bill);
  return (
    <span className="inline-flex items-center gap-1.5 flex-wrap">
      <span
        className={`inline-flex items-center px-2 py-0.5 rounded-sm text-[9px] font-bold uppercase tracking-wider whitespace-nowrap ${
          label === "Reconciled"
            ? "bg-emerald-100 text-emerald-700"
            : "bg-amber-100 text-amber-700"
        }`}
      >
        {label}
      </span>
      {ageDays !== null && label === "Provisional" && (
        <span className="text-[10px] font-semibold text-amber-700 whitespace-nowrap">
          No bank match · {ageDays} days
        </span>
      )}
    </span>
  );
}

// Distinct line-item categories joined by " · ", or "—" when none exist.
export function ShoppingBillsPage() {
  const { getToken } = useAuth();

  const [receipts, setReceipts] = useState<SavedBill[]>([]);
  const [listRows, setListRows] = useState<BackendTransaction[]>([]);

  const [categories, setCategories] = useState<Category[]>([]);

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [uploadLoading, setUploadLoading] = useState(false);
  const [uploadStep, setUploadStep] = useState("");
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [billDraft, setBillDraft] = useState<{ file: File; data: ReceiptData } | null>(null);
  const [confirmingDraft, setConfirmingDraft] = useState(false);
  const [draftError, setDraftError] = useState<string | null>(null);
  const draftImageUrl = useMemo(() => billDraft ? URL.createObjectURL(billDraft.file) : null, [billDraft?.file]);
  useEffect(() => () => { if (draftImageUrl) URL.revokeObjectURL(draftImageUrl); }, [draftImageUrl]);
  // Id of the receipt currently committing its staged edits to the server.
  const [savingId, setSavingId] = useState<string | null>(null);

  // Id of the receipt currently being removed (server-side DELETE in flight).
  const [removingId, setRemovingId] = useState<string | null>(null);

  const financeService = useMemo(
    () => new FinanceService(async () => getToken({ template: "AspNetToken" })),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [getToken],
  );

  // ── Load receipts + categories once on mount ────────────────────────
  useEffect(() => {
    let active = true;

    const loadCategories = async () => {
      try {
        const cats = await financeService.getReceiptCategories();
        if (active) setCategories(cats);
      } catch (e) {
        console.error("Failed to fetch receipt categories:", e);
      }
    };

    const loadReceipts = async () => {
      try {
        setLoading(true);
        setLoadError(null);
        const all = await financeService.getTransactions();
        if (!active) return;
        const filtered = billsForReconciliation(all);
        setListRows(filtered);
        const mapped = filtered.map((row) =>
          backendTransactionToSavedBill(row, (currency) => financeService.mapCurrency(currency)));
        setReceipts(mapped.sort(compareNewestFirst));
      } catch (e) {
        if (active) setLoadError(e instanceof Error ? e.message : String(e));
      } finally {
        if (active) setLoading(false);
      }
    };

    void loadCategories();
    void loadReceipts();
    return () => {
      active = false;
    };
  }, [financeService]);

  // ── Upload → inject new bill at top ────────────────────────────────
  const handleUpload = useCallback(
    async (file: File) => {
      setUploadLoading(true);
      setUploadStep("Reading image metadata...");
      setUploadError(null);

      const steps = [
        "Detecting alignment & text flow (OCR)...",
        "Classifying columns & monetary indices...",
        "Running OCR & parsing models...",
      ];
      let stepIdx = 0;
      const timer = setInterval(() => {
        if (stepIdx < steps.length) setUploadStep(steps[stepIdx++]);
      }, 1200);

      try {
        const { data } = await processReceiptWithDetails(file, async () =>
          getToken({ template: "AspNetToken" }),
        );
        clearInterval(timer);
        setBillDraft({ file, data });
        setDraftError(null);
        setUploadStep("");
      } catch (e) {
        clearInterval(timer);
        setUploadStep("");
        const message = e instanceof Error ? e.message : String(e);
        setUploadError(message);
      } finally {
        setUploadLoading(false);
      }
    },
    [getToken],
  );

  const handleConfirmDraft = useCallback(async (corrected: ReceiptData) => {
    if (!billDraft) return;
    setConfirmingDraft(true);
    setDraftError(null);
    try {
      const saved = await financeService.confirmReceipt(billDraft.file, corrected);
      const mapCurrency = (currency: string) => financeService.mapCurrency(currency);
      const bill = backendTransactionToSavedBill(saved, mapCurrency);
      setReceipts((previous) => [bill, ...previous].sort(compareNewestFirst));
      setListRows((previous) => [saved, ...previous]);
      setBillDraft(null);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setDraftError(message);
      throw error;
    } finally {
      setConfirmingDraft(false);
    }
  }, [billDraft, financeService]);

  // One complete Bill save owns header, item additions/edits/deletions and Matching.
  const handleSave = useCallback(
    async (bill: SavedBill, editedBill: ReceiptData) => {
      setSavingId(bill.id);
      setUploadError(null);
      try {
        const financeService = new FinanceService(async () =>
          getToken({ template: "AspNetToken" }),
        );

        // One complete save returns bank-authoritative facts if this edit reconciles the Bill.
        const saved = await financeService.updateTransaction({
          id: bill.id,
          type: "Expense",
          paymentMethodId: null,
          toPaymentMethodId: null,
          timestamp: editedBill.timestamp || null,
          amount: editedBill.totalAmount,
          currency: editedBill.currency || "UAH",
          description: editedBill.merchantName || "",
          additionalNotes: editedBill.additionalNotes ?? null,
          balanceAfter: null,
          dataOrigin: "Receipt",
          items: editedBill.items.map((item) => ({
            id: item.id,
            name: item.name,
            fullName: item.fullName || item.name,
            category: item.category ?? null,
            subcategory: item.subcategory ?? null,
            quantity: item.quantity,
            pricePerUnit: item.unitPrice,
            totalPrice: item.totalPrice,
          })),
        });
        if (!saved) throw new Error("This Bill no longer exists.");
        const withItems = backendTransactionToSavedBill(saved, (currency) => financeService.mapCurrency(currency));

        setReceipts((prev) =>
          prev
            .map((b) => (b.id === bill.id ? withItems : b))
            .sort(compareNewestFirst),
        );
        setListRows((prev) => prev.map((row) => row.id === bill.id ? saved : row));
      } catch (e) {
        const message = e instanceof Error ? e.message : String(e);
        setUploadError(message);
        throw e; // Keep the panel's draft open when the complete save fails.
      } finally {
        setSavingId(null);
      }
    },
    [getToken],
  );

  // ── Remove transaction (top-level, immediate) ─────────────────────
  // Fired once by BillReportPanel when the user chooses "Remove" from the ⋮
  // menu (after a browser confirmation). Issues the transaction DELETE and
  // drops the row. This is a top-level action, never staged behind Save.
  const handleDelete = useCallback(
    async (id: string) => {
      setRemovingId(id);
      setUploadError(null);
      try {
        await financeService.deleteTransaction(id);

        // Drop the row and clear its auxiliary state so nothing lingers.
        setReceipts((prev) => prev.filter((b) => b.id !== id));
        setListRows((prev) => prev.filter((row) => row.id !== id));
      } catch (e) {
        const message = e instanceof Error ? e.message : String(e);
        setUploadError(message);
      } finally {
        setRemovingId(null);
      }
    },
    [financeService],
  );

  if (loading) {
    return (
      <div className="w-full flex justify-center items-center py-24">
        <Loader2 className="w-6 h-6 animate-spin text-slate-500" />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50 font-mono">
      <div className="max-w-4xl mx-auto w-full px-8 py-8">
        {/* HEADER */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between border-b border-slate-200 pb-4 gap-2">
          <div>
            <div className="flex items-center space-x-2 text-xs text-slate-500 uppercase tracking-widest">
              <span>MODULE // OCR RECEIPT SCANNER</span>
            </div>
            <h1 className="text-2xl font-bold text-zinc-900 tracking-tight mt-0.5">
              AUTOMATED OCR INVOICE EXTRACTION
            </h1>
          </div>

          <p className="text-xs text-slate-500 sm:text-right max-w-sm">
            Your receipts, organized and ready to review. Edits stay local until
            saved; delete an item to remove it permanently.
          </p>
        </div>

        {/* DROPZONE / SCANNER AREA */}
        <div className="mt-6 flex justify-center">
          <UploadZone
            onImageSelected={handleUpload}
            isLoading={uploadLoading}
            loadingStep={uploadStep}
            error={uploadError}
            onFileTypeError={(msg) => setUploadError(msg)}
            onClearImage={() => setUploadError(null)}
          />
        </div>

        {billDraft && (
          <section aria-label="Bill Draft preview" className="mt-5 bg-white border border-slate-300 rounded-sm p-4 space-y-3">
            <div className="flex items-center justify-between text-xs font-bold uppercase tracking-wider">
              <span>Bill Draft · not saved</span>
              <button type="button" onClick={() => { setBillDraft(null); setDraftError(null); }} className="border px-3 py-1">Cancel</button>
            </div>
            <img src={draftImageUrl ?? undefined} alt="Receipt preview" className="max-h-64 max-w-full object-contain mx-auto" />
            <BillReportPanel
              receiptData={billDraft.data}
              categories={categories}
              draft
              saving={confirmingDraft}
              saveError={draftError ?? undefined}
              onSave={handleConfirmDraft}
              expandable={false}
              currency={billDraft.data.currency}
            />
          </section>
        )}

        {loadError && (
          <div className="mt-4 text-xs font-semibold text-red-700 bg-red-50 border border-red-200 rounded px-3 py-2">
            Could not load your bills: {loadError}
          </div>
        )}

        {/* EXTRACTED BILLS TABLE */}
        <div className="mt-6 bg-white border border-slate-300 rounded-sm overflow-hidden">
          <div className="flex items-center justify-between border-b border-slate-200 px-5 py-3">
            <span className="text-xs font-bold uppercase tracking-wider text-zinc-900">
              PROCESSED RECEIPTS ({receipts.length})
            </span>
            <span className="text-[10px] text-slate-500">
              REAL DATA · SERVER-SYNCED
            </span>
          </div>

          <TransactionList
            transactions={listRows}
            label="Processed receipts"
            emptyMessage="No shopping bills yet. Upload a receipt photo above to add your first bill."
            renderRow={(row) => {
              const bill = receipts.find((candidate) => candidate.id === row.id);
              if (!bill) return null;
              return <>
                {removingId === bill.id && <div className="flex items-center gap-2 text-xs text-slate-600"><Loader2 className="w-4 h-4 animate-spin" /> Removing...</div>}
                {savingId === bill.id && <div className="flex items-center gap-2 text-xs text-slate-600"><Loader2 className="w-4 h-4 animate-spin" /> Saving changes...</div>}
                <BillReportPanel
                  receiptData={bill}
                  badge={reconciliationBadge(bill)}
                  moneyDelta={moneyDeltaOf(bill)}
                  categories={categories}
                  onSave={(editedBill) => handleSave(bill, editedBill)}
                  onDelete={(id) => handleDelete(id)}
                  currency={bill.currency}
                  expandable={true}
                />
              </>;
            }}
          />
        </div>
      </div>
    </div>
  );
}

export default ShoppingBillsPage;
