import React, { useCallback, useEffect, useMemo, useState } from "react";
import UploadZone from "../../components/UploadZone/UploadZone";
import BillReportPanel from "../../components/BillReportPanel/BillReportPanel";
import FinanceService from "../../services/FinanceService";
import { backendTransactionToSavedBill, processReceiptWithDetails } from "../../hooks/useProcessReceipt";
import type {
  Category,
  ReceiptData,
  SavedBill,
} from "../../types/receiptTypes";
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
 * and committed once, on Save Refinements: one header PUT /transactions/{id}
 * plus parallel PUT per edited line item and parallel DELETE for locally-
 * deleted items (aggregated). Deleting an item removes it from the panel right
 * away but its server DELETE is staged with the save payload — never fired on
 * its own. No onChange/onDeleteItem delegation: the panel owns staging.
 */

// Newest-first comparison: createdAt (ISO) wins, else timestamp fallback.
function compareNewestFirst(a: SavedBill, b: SavedBill): number {
  const ta = a.createdAt || a.timestamp || "";
  const tb = b.createdAt || b.timestamp || "";
  return tb.localeCompare(ta);
}

// Return a copy of a record with one key removed (no-op if absent), used to
// clear a receipt's tracked expanded/loading state on delete.
function stripKey<T>(record: Record<string, T>, key: string): Record<string, T> {
  if (!(key in record)) return record;
  const next = { ...record };
  delete next[key];
  return next;
}

// Distinct line-item categories joined by " · ", or "—" when none exist.
export function ShoppingBillsPage() {
  const { getToken } = useAuth();

  const [receipts, setReceipts] = useState<SavedBill[]>([]);

  const [categories, setCategories] = useState<Category[]>([]);

  // Per-row expansion is owned by each BillReportPanel (self-contained card).
  // The parent only tracks which rows are expanded to drive lazy item loading
  // and the per-row loading/deleting indicators — not for rendering.
  const [expandedIds, setExpandedIds] = useState<Record<string, boolean>>({});

  // Report a row's expansion back to its panel. Guard against no-op toggles so
  // we only refetch once per expand (items are cached via itemsLoaded).
  const handleExpandChange = useCallback(
    (billId: string) => (isExpanded: boolean) => {
      if (expandedIds[billId] === isExpanded) return;
      setExpandedIds((prev) => ({ ...prev, [billId]: isExpanded }));
    },
    [expandedIds],
  );

  // Transactions come back without their line items (backend returns them
  // item-less). Track which expanded receipts already had their items loaded
  // so we don't re-fetch on every expand/collapse toggle.
  const [itemsLoaded, setItemsLoaded] = useState<Record<string, boolean>>({});

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
        const filtered = all.filter((t) => t.dataOrigin === "Receipt");
        const mapped: SavedBill[] = filtered.map((t) => ({
          id: t.id,
          merchantName:
            t.description !== "Receipt purchase" ? t.description : undefined,
          timestamp: t.timestamp ?? undefined,
          totalAmount: Number(t.amount),
          currency: financeService.mapCurrency(t.currency),
          additionalNotes: t.additionalNotes ?? undefined,
          createdAt: t.createdAt || "",
          tags: [],
          items: (t.items || []).map((it) => ({
            id: it.id ?? undefined,
            name: it.name ?? "",
            fullName: it.fullName ?? "",
            quantity: Number(it.quantity),
            unitPrice: Number(it.pricePerUnit),
            totalPrice: Number(it.totalPrice),
            category: it.category ?? undefined,
            subcategory: it.subcategory ?? undefined,
          })),
        }));
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

  // ── Fetch line items for every expanded receipt that isn't loaded yet ───
  // Transactions come back without their items by design. Each BillReportPanel
  // reports its own expansion via onExpandChange; we batch-load the newly
  // expanded rows here and cache per id so we don't re-fetch on toggle.
  useEffect(() => {
    const pending = receipts
      .filter((b) => expandedIds[b.id] && !itemsLoaded[b.id])
      .map((b) => b.id);

    if (pending.length === 0) return;

    let active = true;

    void Promise.all(
      pending.map((billId) =>
        financeService
          .getTransactionItems(billId)
          .then((backendItems) => {
            const lineItems = backendItems.map((it) => ({
              id: it.id ?? undefined,
              name: it.name ?? "",
              fullName: it.fullName ?? "",
              quantity: Number(it.quantity),
              unitPrice: Number(it.pricePerUnit),
              totalPrice: Number(it.totalPrice),
              category: it.category ?? undefined,
              subcategory: it.subcategory ?? undefined,
            }));

            if (!active) return;

            // Merge fetched server items into the persisted bill.
            setReceipts((prev) =>
              prev
                .map((b) =>
                  b.id === billId ? { ...b, items: lineItems } : b,
                )
                .sort(compareNewestFirst),
            );
          })
          .catch((e) => console.error("Failed to fetch line items:", e)),
      ),
    ).finally(() => {
      if (!active) return;
      setItemsLoaded((prev) => {
        const next = { ...prev };
        for (const id of pending) next[id] = true;
        return next;
      });
    });

    return () => {
      active = false;
    };
  }, [expandedIds, itemsLoaded, receipts, financeService]);

  // Effective data for a receipt. Edits are staged inside the panel's own
  // draft; this is just the committed bill (or its freshly-loaded items).
  const effectiveData = (bill: SavedBill): SavedBill => bill;

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
      setListRows((previous) => [transactionRowFromBill(bill), ...previous]);
      setBillDraft(null);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setDraftError(message);
      throw error;
    } finally {
      setConfirmingDraft(false);
    }
  }, [billDraft, financeService]);

  // ── Save-on-press commit (server-side, aggregate of PUT/PUT/DELETE) ───
  // Fired once by BillReportPanel when the user presses "Save Refinements".
  // Edits + staged item deletions are persisted here together; a DELETE is
  // never issued outside of this save. Errors across the three call groups
  // are aggregated by Promise.all (first failure wins).
  const handleSave = useCallback(
    async (bill: SavedBill, editedBill: ReceiptData) => {
      setSavingId(bill.id);
      setUploadError(null);
      try {
        const financeService = new FinanceService(async () =>
          getToken({ template: "AspNetToken" }),
        );

        // 1. Header fields — merchantName→Description, totalAmount→Amount,
        // timestamp, currency; paymentMethod stays null for receipt bills.
        await financeService.updateTransaction({
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
        });

        const original = bill.items; // server-owned, carry ids
        const incoming = editedBill.items;
        const incomingById = new Map(
          incoming.filter((i) => i.id).map((i) => [i.id, i]),
        );

        // 2. Parallel PUT for each edited line item that has a backend id.
        const putOps = incoming
          .filter((item) => item.id)
          .map((item) =>
            financeService.updateTransactionItem(item.id!, {
              id: item.id!,
              name: item.name,
              fullName: item.fullName || item.name,
              category: item.category ?? null,
              subcategory: item.subcategory ?? null,
              quantity: item.quantity,
              pricePerUnit: item.unitPrice,
              totalPrice: item.totalPrice,
              origin: "ManualInput",
            }),
          );

        // 3. Parallel DELETE for locally-deleted items (staged in the draft).
        const deleteOps = original
          .filter((origItem) => !!origItem.id && !incomingById.has(origItem.id))
          .map((origItem) =>
            financeService
              .deleteTransactionItem(origItem.id!)
              .then(() => true),
          );

        // 4. Parallel POST for freshly-added line items (no backend id yet).
        const createOps = incoming
          .filter((item) => !item.id)
          .map((item) =>
            financeService
              .createTransactionItem(bill.id, {
                name: item.name,
                fullName: item.fullName || item.name,
                category: item.category ?? null,
                subcategory: item.subcategory ?? null,
                quantity: item.quantity,
                pricePerUnit: item.unitPrice,
                totalPrice: item.totalPrice,
              })
              .then(() => true),
          );

        // Aggregate errors across the three call categories.
        await Promise.all([...putOps, ...deleteOps, ...createOps]);

        // 5. Refresh this bill from server so the panel shows committed data
        //    (its own effect resets the staged draft on receiptData identity).
        const backendItems = await financeService.getTransactionItems(bill.id);
        if (!backendItems) return;
        const withItems: SavedBill = {
          ...bill,
          merchantName: editedBill.merchantName,
          timestamp: editedBill.timestamp,
          totalAmount: editedBill.totalAmount,
          currency: editedBill.currency,
          additionalNotes: editedBill.additionalNotes,
          items: backendItems.map((it) => ({
            id: it.id ?? undefined,
            name: it.name ?? "",
            fullName: it.fullName ?? "",
            quantity: Number(it.quantity),
            unitPrice: Number(it.pricePerUnit),
            totalPrice: Number(it.totalPrice),
            category: it.category ?? undefined,
            subcategory: it.subcategory ?? undefined,
          })),
        };

        setReceipts((prev) =>
          prev
            .map((b) => (b.id === bill.id ? withItems : b))
            .sort(compareNewestFirst),
        );
      } catch (e) {
        const message = e instanceof Error ? e.message : String(e);
        setUploadError(message);
      } finally {
        setSavingId(null);
      }
    },
    [getToken],
  );

  // ── Remove transaction (top-level, immediate) ─────────────────────
  // Fired once by BillReportPanel when the user chooses "Remove" from the ⋮
  // menu (after a browser confirmation). Issues the transaction DELETE and
  // drops the row — clearing any tracked expanded/loading state so no ghost
  // entry can reappear. This is a top-level action, never staged behind Save.
  const handleDelete = useCallback(
    async (id: string) => {
      setRemovingId(id);
      setUploadError(null);
      try {
        await financeService.deleteTransaction(id);

        // Drop the row and clear its auxiliary state so nothing lingers.
        setReceipts((prev) => prev.filter((b) => b.id !== id));
        setExpandedIds((prev) => stripKey(prev, id));
        setItemsLoaded((prev) => stripKey(prev, id));
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

          {/* Each bill is now a self-contained BillReportPanel card. The
              panel owns its own collapse/expand; the parent only tracks which
              rows are expanded to drive lazy item loading + per-row indicators. */}
          {receipts.length === 0 ? (
            <div className="px-5 py-16 text-center">
              <p className="text-sm font-bold text-zinc-900 mb-1">
                No shopping bills yet
              </p>
              <p className="text-xs text-slate-500 max-w-xs mx-auto">
                Upload a receipt photo above to add your first bill. Anything
                you change stays local until it is saved to the server.
              </p>
            </div>
          ) : (
            <div className="flex flex-col gap-3 p-4">
              {receipts.map((bill) => {
                const isOpen = expandedIds[bill.id] ?? false;

                return (
                  <React.Fragment key={bill.id}>
                    {removingId === bill.id && (
                      <div className="flex items-center gap-2 text-xs text-slate-600">
                        <Loader2 className="w-4 h-4 animate-spin" /> Removing
                        ...
                      </div>
                    )}
                    {savingId === bill.id && (
                      <div className="flex items-center gap-2 text-xs text-slate-600">
                        <Loader2 className="w-4 h-4 animate-spin" /> Saving
                        changes...
                      </div>
                    )}
                    {isOpen && !itemsLoaded[bill.id] && (
                      <div className="flex items-center gap-2 text-xs text-slate-600">
                        <Loader2 className="w-4 h-4 animate-spin" /> Loading
                        line items...
                      </div>
                    )}
                    <BillReportPanel
                      receiptData={effectiveData(bill)}
                      categories={categories}
                      onSave={(editedBill) => handleSave(bill, editedBill)}
                      onDelete={(id) => handleDelete(id)}
                      currency={bill.currency}
                      expandable={true}
                      onExpandChange={handleExpandChange(bill.id)}
                    />
                  </React.Fragment>
                );
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default ShoppingBillsPage;
