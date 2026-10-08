import React, { useCallback, useEffect, useMemo, useState } from "react";
import UploadZone from "../../components/UploadZone/UploadZone";
import BillReportPanel from "../../components/BillReportPanel/BillReportPanel";
import BillImageViewer from "../../components/BillImageViewer/BillImageViewer";
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
import { unstable_usePrompt as usePrompt } from "react-router-dom";

/** Uploads and Bill Draft confirmation stay page-owned; saved Bills use the common card. */

// Newest-first comparison: createdAt (ISO) wins, else timestamp fallback.
function compareNewestFirst(a: BackendTransaction, b: BackendTransaction): number {
  const ta = a.createdAt || a.timestamp || "";
  const tb = b.createdAt || b.timestamp || "";
  return tb.localeCompare(ta);
}

async function imageContentHash(file: File): Promise<string> {
  const bytes = typeof file.arrayBuffer === "function"
    ? await file.arrayBuffer()
    : await new Promise<ArrayBuffer>((resolve, reject) => {
        const reader = new FileReader();
        reader.onerror = () => reject(reader.error ?? new Error("Could not read image."));
        reader.onload = () => reader.result instanceof ArrayBuffer
          ? resolve(reader.result)
          : reject(new Error("Could not read image bytes."));
        reader.readAsArrayBuffer(file);
      });
  const digest = await window.crypto.subtle.digest("SHA-256", bytes);
  return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, "0")).join("");
}

export function ShoppingBillsPage() {
  const { getToken } = useAuth();

  const [listRows, setListRows] = useState<BackendTransaction[]>([]);

  const [categories, setCategories] = useState<Category[]>([]);
  const [methods, setMethods] = useState<BackendPaymentMethod[]>([]);

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [preparingSelection, setPreparingSelection] = useState(false);
  type QueueEntry = { id: number; file: File; status: "waiting" | "extracting" | "ready" | "failed" | "confirming" | "saved"; data?: ReceiptData; error?: string };
  const [queue, setQueue] = useState<QueueEntry[]>([]);
  const [activeId, setActiveId] = useState<number | null>(null);
  const [confirmingDraft, setConfirmingDraft] = useState(false);
  const [draftError, setDraftError] = useState<string | null>(null);
  const hasUnsavedWork = queue.some(entry => entry.status !== "saved");
  usePrompt({ when: hasUnsavedWork, message: "Leave this page and discard unfinished Bill Images and drafts? A Bill confirmation already submitted may still finish. Saved Bills will remain." });
  const activeEntry = queue.find(entry => entry.id === activeId && entry.status !== "saved");
  const draftImageUrl = useMemo(() => activeEntry ? URL.createObjectURL(activeEntry.file) : null, [activeEntry?.id]);
  useEffect(() => () => { if (draftImageUrl) URL.revokeObjectURL(draftImageUrl); }, [draftImageUrl]);
  const queueRef = React.useRef<QueueEntry[]>([]);
  const nextId = React.useRef(0);
  const generation = React.useRef(0);
  const extractionInFlight = React.useRef(false);
  const selectionInFlight = React.useRef(false);
  const seenImageHashes = React.useRef(new Set<string>());
  const setEntries = useCallback((update: (entries: QueueEntry[]) => QueueEntry[]) => {
    const next = update(queueRef.current);
    queueRef.current = next;
    setQueue(next);
  }, []);
  useEffect(() => () => { generation.current += 1; }, []);
  useEffect(() => {
    if (!hasUnsavedWork) return;
    const protectUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", protectUnload);
    return () => window.removeEventListener("beforeunload", protectUnload);
  }, [hasUnsavedWork]);
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

  // Process a fixed selection sequentially; review and confirmation can overlap later extraction.
  const processNext = useCallback(async () => {
    if (extractionInFlight.current) return;
    const candidate = queueRef.current.find(entry => entry.status === "waiting");
    if (!candidate) return;
    extractionInFlight.current = true;
    const requestGeneration = generation.current;
    setEntries(entries => entries.map(entry => entry.id === candidate.id ? { ...entry, status: "extracting", error: undefined } : entry));
    try {
      const { data } = await processReceiptWithDetails(candidate.file, async () => getToken({ template: "AspNetToken" }));
      if (requestGeneration !== generation.current) return;
      setEntries(entries => entries.map(entry => entry.id === candidate.id ? { ...entry, status: "ready", data, error: undefined } : entry));
      setActiveId(current => current ?? candidate.id);
    } catch (error) {
      if (requestGeneration !== generation.current) return;
      const message = error instanceof Error ? error.message : String(error);
      setEntries(entries => entries.map(entry => entry.id === candidate.id ? { ...entry, status: "failed", error: message } : entry));
      setUploadError(message);
    } finally {
      if (requestGeneration === generation.current) {
        extractionInFlight.current = false;
        if (queueRef.current.some(entry => entry.status === "waiting")) void processNext();
      }
    }
  }, [getToken, setEntries]);

  const handleUpload = useCallback(async (files: File[]) => {
    if (queueRef.current.length || selectionInFlight.current) return;
    selectionInFlight.current = true;
    setPreparingSelection(true);
    try {
      const accepted: File[] = [];
      const hashes = new Set<string>();
      const duplicates: string[] = [];
      for (const file of files) {
        const hash = await imageContentHash(file);
        if (seenImageHashes.current.has(hash) || hashes.has(hash)) duplicates.push(file.name);
        else { hashes.add(hash); accepted.push(file); }
      }
      hashes.forEach(hash => seenImageHashes.current.add(hash));
      if (duplicates.length) setUploadError(previous => [previous, `Duplicate images not added: ${duplicates.join(", ")}`].filter(Boolean).join(" "));
      if (!accepted.length) return;
      generation.current += 1;
      const entries = accepted.map(file => ({ id: nextId.current++, file, status: "waiting" as const }));
      queueRef.current = entries;
      setQueue(entries);
      void processNext();
    } catch (error) {
      setUploadError(error instanceof Error ? `Could not check image duplicates: ${error.message}` : "Could not check image duplicates.");
    } finally {
      selectionInFlight.current = false;
      setPreparingSelection(false);
    }
  }, [processNext]);

  const handleDraftChange = useCallback((id: number, data: ReceiptData | null) => {
    if (!data) return;
    setEntries(entries => entries.map(entry => entry.id === id ? { ...entry, data } : entry));
  }, [setEntries]);

  const handleConfirmDraft = useCallback(async (corrected: ReceiptData) => {
    const entry = queueRef.current.find(item => item.id === activeId);
    if (!entry?.data || confirmingDraft) return;
    setConfirmingDraft(true);
    setDraftError(null);
    setEntries(entries => entries.map(item => item.id === entry.id ? { ...item, status: "confirming", data: corrected } : item));
    try {
      const saved = await financeService.confirmReceipt(entry.file, corrected);
      await refreshRows(saved);
      setEntries(entries => entries.map(item => item.id === entry.id ? { ...item, status: "saved" } : item));
      const next = queueRef.current.find(item => item.status === "ready" && item.id !== entry.id);
      setActiveId(next?.id ?? null);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setDraftError(message);
      setEntries(entries => entries.map(item => item.id === entry.id ? { ...item, status: "ready", error: message, data: corrected } : item));
      throw error;
    } finally {
      setConfirmingDraft(false);
    }
  }, [activeId, confirmingDraft, financeService, refreshRows, setEntries]);

  useEffect(() => {
    if (queue.length && queue.every(entry => entry.status === "saved")) {
      queueRef.current = [];
      setQueue([]);
      setActiveId(null);
    }
  }, [queue]);

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
          {!queue.length && <UploadZone
            onImagesSelected={handleUpload}
            isLoading={preparingSelection}
            loadingStep={preparingSelection ? "Checking selected images..." : ""}
            error={uploadError}
            onFileTypeError={(msg) => setUploadError(msg)}
            onClearImage={() => setUploadError(null)}
          />}
        </div>
        {uploadError && queue.length > 0 && <div role="alert" className="mt-3 p-3 bg-rose-50 border border-rose-100 rounded-sm text-xs text-rose-700">{uploadError}</div>}

        {!!queue.length && <section aria-label="Bill Image queue" className="mt-5 bg-white border border-slate-300 rounded-sm">
          <div className="flex justify-between border-b border-slate-200 px-5 py-3 text-xs font-bold uppercase tracking-wider"><span>Bill Image queue</span><span>{queue.filter(item => item.status === "saved").length} saved · {queue.filter(item => item.status === "ready").length} ready · {queue.filter(item => item.status === "extracting").length} extracting · {queue.filter(item => item.status === "waiting").length} waiting · {queue.filter(item => item.status === "failed").length} failed</span></div>
          {queue.map(entry => <div key={entry.id} className={`flex items-center gap-3 px-5 py-2 border-b border-slate-100 ${entry.id === activeId ? "bg-slate-100" : ""}`}>
            <button type="button" disabled={!entry.data || entry.status === "confirming" || entry.status === "saved"} onClick={() => {setActiveId(entry.id); setDraftError(entry.error ?? null);}} className="flex-1 text-left text-xs underline disabled:no-underline">{entry.file.name}</button>
            <span className="text-[10px] uppercase text-slate-500">{entry.error ? `Failed: ${entry.error}` : entry.status}</span>
            {entry.status === "failed" && <button type="button" disabled={extractionInFlight.current} onClick={() => {setEntries(items => items.map(item => item.id === entry.id ? {...item, status: "waiting", error: undefined} : item)); void processNext();}} className="border px-2 py-1 text-xs">Retry</button>}
          </div>)}
          <div className="flex justify-end p-3"><button type="button" disabled={confirmingDraft} onClick={() => {
            const unsaved = queueRef.current.filter(item => item.status !== "saved").length;
            if (!window.confirm(`End review and discard ${unsaved} unsaved image${unsaved === 1 ? "" : "s"} and Bill Draft${unsaved === 1 ? "" : "s"}? Saved Bills will remain.`)) return;
            generation.current += 1; extractionInFlight.current = false; queueRef.current = []; setQueue([]); setActiveId(null); setDraftError(null); setUploadError(null);
          }} className="border px-3 py-1 text-xs">End review</button></div>
        </section>}

        {activeEntry?.data && (
          <section aria-label="Bill Draft preview" className="mt-5 bg-white border border-slate-300 rounded-sm p-4 space-y-3">
            <div className="flex items-center justify-between text-xs font-bold uppercase tracking-wider">
              <span>Bill Draft · not saved · {activeEntry.file.name}</span>
            </div>
            {draftImageUrl && <BillImageViewer key={activeEntry.id} src={draftImageUrl} alt={`Original Bill Image: ${activeEntry.file.name}`} />}
            <BillReportPanel
              key={activeEntry.id}
              receiptData={activeEntry.data}
              onDraft={data => handleDraftChange(activeEntry.id, data)}
              categories={categories}
              draft
              saving={confirmingDraft}
              saveError={draftError ?? undefined}
              onSave={handleConfirmDraft}
              expandable={false}
              currency={activeEntry.data.currency}
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
