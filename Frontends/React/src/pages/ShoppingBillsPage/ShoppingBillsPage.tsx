import React, { useCallback, useEffect, useMemo, useState } from "react";
import UploadZone from "../../components/UploadZone/UploadZone";
import BillReportPanel from "../../components/BillReportPanel/BillReportPanel";
import TransactionList from "../../components/TransactionList/TransactionList";
import FinanceService from "../../services/FinanceService";
import { processReceiptWithDetails } from "../../hooks/useProcessReceipt";
import type {
  BackendTransaction,
  Category,
  ReceiptData,
} from "../../types/receiptTypes";
import { billsForReconciliation } from "../../domain/reconciliation";
import { transactionSaveBody } from "../../domain/transactionDetail";
import type { BackendPaymentMethod } from "../../types/paymentMethodTypes";
import { Loader2 } from "lucide-react";
import { useAuth } from "@clerk/clerk-react";

/** Uploads and Bill Draft confirmation stay page-owned; saved Bills use the common card. */

// Newest-first comparison: createdAt (ISO) wins, else timestamp fallback.
function compareNewestFirst(a: BackendTransaction, b: BackendTransaction): number {
  const ta = a.createdAt || a.timestamp || "";
  const tb = b.createdAt || b.timestamp || "";
  return tb.localeCompare(ta);
}

export function ShoppingBillsPage() {
  const { getToken } = useAuth();

  const [listRows, setListRows] = useState<BackendTransaction[]>([]);

  const [categories, setCategories] = useState<Category[]>([]);
  const [methods, setMethods] = useState<BackendPaymentMethod[]>([]);

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
        setListRows(billsForReconciliation(all).sort(compareNewestFirst));
      } catch (e) {
        if (active) setLoadError(e instanceof Error ? e.message : String(e));
      } finally {
        if (active) setLoading(false);
      }
    };

    void (async () => {
      try { const list = await financeService.getPaymentMethods(); if (active) setMethods(list); }
      catch { /* Unresolved methods are displayed as Unknown. */ }
    })();
    void loadCategories();
    void loadReceipts();
    return () => {
      active = false;
    };
  }, [financeService]);

  const refreshRows = useCallback(async (saved: BackendTransaction) => {
    // Update immediately, then refresh other candidate markers as well.
    setListRows(previous => [saved, ...previous.filter(row => row.id !== saved.id)].sort(compareNewestFirst));
    try {
      const rows = billsForReconciliation(await financeService.getTransactions());
      setListRows([saved, ...rows.filter(row => row.id !== saved.id)].sort(compareNewestFirst));
    } catch { /* The save succeeded; retain its returned truth if a subsequent read fails. */ }
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
      await refreshRows(saved);
      setBillDraft(null);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setDraftError(message);
      throw error;
    } finally {
      setConfirmingDraft(false);
    }
  }, [billDraft, financeService, refreshRows]);

  // One complete Bill save owns header, item additions/edits/deletions and Matching.
  const handleSave = useCallback(
    async (bill: BackendTransaction, editedBill: ReceiptData) => {
      setUploadError(null);
      try {
        // One complete save returns bank-authoritative facts if this edit reconciles the Bill.
        const saved = await financeService.updateTransaction(transactionSaveBody(bill, editedBill));
        if (!saved) throw new Error("This Bill no longer exists.");
        await refreshRows(saved);
      } catch (e) {
        const message = e instanceof Error ? e.message : String(e);
        setUploadError(message);
        throw e; // Keep the panel's draft open when the complete save fails.
      }
    },
    [financeService, refreshRows],
  );

  // ── Remove transaction (top-level, immediate) ─────────────────────
  // Fired once by the common card when the user chooses "Remove" from the ⋮
  // menu (after a browser confirmation). Issues the transaction DELETE and
  // drops the row. This is a top-level action, never staged behind Save.
  const handleDelete = useCallback(
    async (id: string) => {
      setUploadError(null);
      try {
        await financeService.deleteTransaction(id);

        // Drop the row and clear its auxiliary state so nothing lingers.
        setListRows(previous => previous.filter(row => row.id !== id).map(row => ({
          ...row, reviewCandidates: row.reviewCandidates?.filter(candidate => candidate.id !== id),
        })));
      } catch (e) {
        const message = e instanceof Error ? e.message : String(e);
        setUploadError(message);
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
              PROCESSED RECEIPTS ({listRows.length})
            </span>
            <span className="text-[10px] text-slate-500">
              REAL DATA · SERVER-SYNCED
            </span>
          </div>

          <TransactionList
            transactions={listRows}
            label="Processed receipts"
            emptyMessage="No shopping bills yet. Upload a receipt photo above to add your first bill."
            categories={categories}
            paymentMethodName={id => methods.find(method => method.id === id)?.name || null}
            paymentMethodCurrency={id => methods.find(method => method.id === id)?.currency || null}
            onSave={handleSave}
            onDelete={row => { if (window.confirm("Remove this transaction? This cannot be undone.")) void handleDelete(row.id); }}
          />
        </div>
      </div>
    </div>
  );
}

export default ShoppingBillsPage;
