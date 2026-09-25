import React, { useEffect, useMemo, useState } from "react";
import type { ReceiptData, Category, LineItem } from "../../types/receiptTypes";
import { MoreVertical, X, Save, List } from "lucide-react";

interface BillReportPanelProps {
  receiptData: ReceiptData;
  categories?: Category[];
  /**
   * Commit-on-save callback. Fired exactly once when the user presses
   * "Save Refinements" with a fully populated, drafted bill. No other prop
   * exposes editing to the parent — all edits stay staged in the panel's local
   * draft until onSave fires (save-on-press). Optional: callers that only use
   * the panel for display can omit it; Save still exits edit mode defensively.
   */
  onSave?: (bill: ReceiptData) => void | Promise<void>;
  draft?: boolean;
  saving?: boolean;
  saveError?: string;
  /**
   * Optional live-draft reporter used purely for display wiring (e.g. a footer
   * snapshot). It never triggers any server write and is not the commit
   * path — onSave remains the single, once-per-session persistence seam.
   * Called with `null` when editing ends (Save committed or Cancel discarded)
   * so callers can revert any staging UI to the committed bill.
   */
  onDraft?: (data: ReceiptData | null) => void;
  /**
   * Remove-the-transaction callback. Fired exactly once when the user chooses
   * "Remove" from the header ⋮ menu (after a browser confirmation). The panel
   * performs no server write itself — the parent issues the transaction DELETE.
   * Optional: callers that cannot remove bills can omit it.
   */
  onDelete?: (id: string) => void;
  currency?: string;
  expandable?: boolean;
  onExpandChange?: (expanded: boolean) => void;
  badge?: React.ReactNode;
  moneyDelta?: number | null;
}

export default function BillReportPanel({
  receiptData,
  categories = [],
  onSave,
  onDraft,
  onDelete,
  currency,
  expandable = false,
  onExpandChange,
  badge,
  moneyDelta,
  draft = false,
  saving = false,
  saveError,
}: BillReportPanelProps) {
  // The panel owns its own edit draft. Live edits (and item deletions) are
  // staged here and never mutate the parent's receiptData until onSave fires.
  const [localReceipt, setLocalReceipt] = useState<ReceiptData>(() => ({
    ...receiptData,
  }));

  const [editMode, setEditMode] = useState<boolean>(draft);

  // Expand/collapse is owned locally (the panel is self-contained) and also
  // reported to the parent purely for display wiring.
  const [expanded, setExpanded] = useState<boolean>(!expandable);

  // The ⋮ action-menu open state.
  const [menuOpen, setMenuOpen] = useState<boolean>(false);

  // Keep the draft in sync when the underlying data identity changes (e.g. a
  // new bill is loaded or committed back from the server).
  React.useEffect(() => {
    setLocalReceipt({ ...receiptData });
  }, [receiptData]);

  // Close the ⋮ menu on any click outside of it.
  useEffect(() => {
    if (!menuOpen) return;
    const closeOnOutsideClick = (event: MouseEvent) => {
      const target = event.target as HTMLElement | null;
      if (target && !target.closest('[aria-label="More options"]')) {
        setMenuOpen(false);
      }
    };
    document.addEventListener("click", closeOnOutsideClick);
    return () => document.removeEventListener("click", closeOnOutsideClick);
  }, [menuOpen]);

  const emitDraft = (next: ReceiptData) => {
    setLocalReceipt(next);
    onDraft?.(next);
  };

  // ── Local draft mutations (staged; no server write, no parent mutation) ──

  const updateField = (field: keyof ReceiptData, value: unknown) => {
    emitDraft({ ...localReceipt, [field]: value });
  };

  const updateItem = (index: number, updates: Partial<LineItem>) => {
    const items = [...localReceipt.items];
    if (items[index]) {
      items[index] = { ...items[index], ...updates };
      emitDraft({ ...localReceipt, items });
    }
  };

  const addNewLine = () => {
    const newItem: LineItem = {
      name: "",
      fullName: "",
      quantity: 1,
      unitPrice: 0,
      totalPrice: 0,
    };
    emitDraft({ ...localReceipt, items: [...localReceipt.items, newItem] });
  };

  const removeLineItem = (index: number) => {
    // Deletion is staged locally in the draft. It disappears from the panel
    // immediately and is only committed (or discarded on Cancel) at save time.
    // No onDeleteItem delegation — nothing outside this panel is mutated.
    emitDraft({
      ...localReceipt,
      items: localReceipt.items.filter((_, i) => i !== index),
    });
  };

  const handleSaveRefinements = () => {
    // Commit once. The full drafted bill is handed to the parent exactly one
    // time; save-on-press means there is no per-keystroke persistence. The
    // staged draft is reset to the committed bill so the panel no longer
    // flags any unsaved edits after a successful save.
    // Total is derived from the items, so persist the recomputed sum rather
    // than the (now stale) committed totalAmount. This is the only path that
    // writes the bill to the parent/server, so the derived total lands here.
    if (draft && !localReceipt.timestamp) return;
    const finish = () => {
      setLocalReceipt({ ...receiptData });
      setEditMode(false);
      onDraft?.(null);
    };
    try {
      const result = onSave?.({ ...localReceipt, totalAmount: computedTotal });
      if (result && typeof (result as Promise<void>).then === "function") {
        void (result as Promise<void>).then(finish).catch(() => {
          // Keep the corrected fields staged for retry.
        });
      } else {
        finish();
      }
    } catch {
      // Keep the corrected fields staged for retry.
    }
  };

  const handleCancelRefinements = () => {
    // Discard all staged edits — revert the draft to the committed bill and
    // leave edit mode without any server write.
    setLocalReceipt({ ...receiptData });
    setEditMode(false);
    onDraft?.(null);
  };

  // Remove the whole transaction: confirm first, then delegate. No staging —
  // this is a top-level action, not a refinement.
  const handleRemoveTransaction = () => {
    if (window.confirm("Remove this transaction? This cannot be undone.")) {
      onDelete?.(receiptData.id ?? "");
    }
  };

  const toggleExpand = () => {
    const next = !expanded;
    setExpanded(next);
    onExpandChange?.(next);
  };

  // ── Derived row values (from the staged draft) ─────────────────────

  // Distinct item categories joined by " · ", or "—" when none. Reflects the
  // staged draft so the body stays consistent with edited items.
  const categorySummary = useMemo(() => {
    const distinct = new Set(
      localReceipt.items
        .map((item) => item.category)
        .filter((c): c is string => !!c),
    );
    if (distinct.size === 0) return "—";
    return Array.from(distinct).join(" · ");
  }, [localReceipt.items]);

  // Whether the staged draft has diverged from the committed bill. The header
  // total is now derived from the items (not a separately editable field), so
  // divergence is detected purely from the editable fields and the item list.
  const hasUnsavedEdits = useMemo(() => {
    const committed = receiptData;
    const staged = localReceipt;
    if (committed.merchantName !== staged.merchantName) return true;
    if (committed.timestamp !== staged.timestamp) return true;
    if (committed.currency !== staged.currency) return true;
    if ((committed.additionalNotes ?? null) !== (staged.additionalNotes ?? null))
      return true;
    if (committed.items.length !== staged.items.length) return true;
    return committed.items.some((item, i) => {
      const stagedItem = staged.items[i];
      if (!stagedItem) return true;
      return (
        item.name !== stagedItem.name ||
        item.fullName !== stagedItem.fullName ||
        item.quantity !== stagedItem.quantity ||
        item.unitPrice !== stagedItem.unitPrice ||
        item.totalPrice !== stagedItem.totalPrice
      );
    });
  }, [receiptData, localReceipt]);

  // Total derived from the staged items (price × quantity summed). This is the
  // single source of truth for the header total while editing; editing an
  // item's price or quantity recomputes it live.
  const computedTotal = useMemo(
    () =>
      localReceipt.items.reduce(
        (sum, it) => sum + (it.unitPrice ?? 0) * (it.quantity ?? 0),
        0,
      ),
    [localReceipt.items],
  );

  const vendor = receiptData.merchantName || "Unnamed bill";
  const amountText = receiptData.totalAmount.toLocaleString();
  const currencyText = currency ?? receiptData.currency;

  // The header row is a clickable expand toggle when the panel is expandable
  // and not editing; in edit mode only the chevron toggles.
  const rowIsToggle = expandable && !editMode;

  // Column span shared by the table header and its rows, so Price, Qty and
  // Cost always sit under their headings. View mode has no delete column, so
  // Category widens to fill all twelve tracks; edit mode hands that track back
  // to the delete button (category + subcategory selects span 2 + 2 = 4).
  const categoryColSpan = editMode ? "col-span-4" : "col-span-5";

  return (
    <div className="w-full font-mono">
      {/* Header row. */}
      <div
        className={`flex items-center justify-between w-full px-5 py-4 bg-slate-50 border border-slate-300 rounded-t-sm transition text-left ${
          rowIsToggle ? "cursor-pointer hover:bg-slate-100" : ""
        }`}
        onClick={rowIsToggle ? toggleExpand : undefined}
      >
        {/* Left group: display fields (normal) or editable header meta (edit). */}
        <div
          className={`flex items-center gap-4 min-w-0 flex-1 ${
            rowIsToggle ? "cursor-pointer" : ""
          }`}
          onClick={rowIsToggle ? toggleExpand : undefined}
        >
          {/* Normal mode: the flat transaction row. */}
          {!editMode && (
            <>
              <div className="flex items-center gap-3 min-w-0">
                <List className="w-4 h-4 text-slate-500 shrink-0" />
                <span className="text-sm font-bold text-zinc-900 uppercase tracking-tight truncate">
                  {vendor}
                </span>
              </div>
              <span className="text-xs text-slate-500 tabular-nums whitespace-nowrap shrink-0">
                {formatReceiptDateTime(receiptData.timestamp)}
              </span>
              {badge && <span className="shrink-0">{badge}</span>}
            </>
          )}

          {/* Edit mode: the header meta becomes inline editable controls.
              Merchant, Date/Time and Currency are editable; Total is shown
              readonly (derived from the staged items). No category, no notes. */}
          {editMode && (
            <div className="flex flex-wrap items-center gap-3 w-full">
              <label className="flex-1 min-w-[160px]">
                <span className="text-[11px] text-slate-600 uppercase block mb-1">
                  Merchant
                </span>
                <input
                  type="text"
                  value={localReceipt.merchantName ?? ""}
                  onChange={(e) =>
                    updateField("merchantName", e.target.value)
                  }
                  className="w-full bg-slate-50 border border-slate-300 px-3 py-2 text-xs rounded-sm focus:outline-none focus:border-zinc-900 transition"
                />
              </label>

              <label>
                <span className="text-[11px] text-slate-600 uppercase mb-1 block">
                  Date / Time
                </span>
                <input
                  type="datetime-local"
                  value={
                    localReceipt.timestamp
                      ? toLocalInputValue(localReceipt.timestamp)
                      : ""
                  }
                  onChange={(e) =>
                    updateField("timestamp", e.target.value || null)
                  }
                  className="bg-slate-50 border border-slate-300 px-2.5 py-2 text-xs rounded-sm focus:outline-none focus:border-zinc-900 transition"
                />
              </label>

              <label>
                <span className="text-[11px] text-slate-600 uppercase mb-1 block">
                  Currency
                </span>
                <select
                  aria-label="Currency"
                  value={currencyText ?? ""}
                  onChange={(e) => updateField("currency", e.target.value)}
                  className="bg-slate-50 border border-slate-300 px-2 py-2 text-xs rounded-sm focus:outline-none focus:border-zinc-900 transition uppercase"
                >
                  <option value="UAH">UAH</option>
                  <option value="USD">USD</option>
                  <option value="EUR">EUR</option>
                </select>
              </label>

              <label className="flex items-end gap-1">
                <span className="text-[11px] text-slate-600 uppercase">
                  Total
                </span>
                <span className="text-sm font-bold text-emerald-700 tabular-nums whitespace-nowrap">
                  {computedTotal.toLocaleString(undefined, {
                    minimumFractionDigits: 2,
                    maximumFractionDigits: 2,
                  })}{" "}
                  <span className="text-[10px] font-semibold text-slate-500 uppercase">
                    {currencyText || ""}
                  </span>
                </span>
              </label>
            </div>
          )}
        </div>

        {/* Right group: amount + actions + chevron. */}
        <div
          className="flex items-center gap-3 shrink-0 ml-4"
          onClick={(e) => e.stopPropagation()}
        >
          {/* Normal mode: the amount sits left of the expand toggle, with the
              ⋮ menu at the far edge. */}
          {!editMode && (
            <div className="shrink-0 text-right">
              <span className="text-sm font-bold text-zinc-900 tabular-nums whitespace-nowrap">
                {amountText}{" "}
                <span className="text-[10px] font-semibold text-slate-500 uppercase">
                  {currencyText || ""}
                </span>
              </span>
              {moneyDelta !== null && moneyDelta !== undefined && (
                <span className="block text-[10px] font-semibold text-amber-700 tabular-nums whitespace-nowrap">
                  Money Delta ·{" "}
                  {moneyDelta.toLocaleString(undefined, {
                    minimumFractionDigits: 2,
                    maximumFractionDigits: 2,
                  })}{" "}
                  {currencyText || ""}
                </span>
              )}
            </div>
          )}

          {hasUnsavedEdits && (
            <span className="inline-flex items-center px-2 py-0.5 rounded-sm bg-amber-100 text-amber-700 text-[9px] font-bold uppercase tracking-wider whitespace-nowrap shrink-0">
              Unsaved
            </span>
          )}

          {expandable && (
            <button
              type="button"
              title={expanded ? "Collapse report" : "Expand report"}
              aria-expanded={expanded}
              aria-label={expanded ? "Collapse report" : "Expand report"}
              onClick={toggleExpand}
              className="inline-flex items-center justify-center w-8 h-8 rounded-sm transition-all cursor-pointer bg-white hover:bg-slate-50 text-slate-400 shadow-sm"
            >
              <svg
                className={`w-4 h-4 transition-transform ${
                  expanded ? "rotate-180" : ""
                }`}
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M19 9l-7 7-7-7"
                />
              </svg>
            </button>
          )}

          {editMode ? (
            <div className="flex items-center gap-2">
              <button
                onClick={handleCancelRefinements}
                type="button"
                className="inline-flex items-center gap-1 text-[11px] font-bold uppercase tracking-wider px-3 py-1.5 rounded-sm transition-all cursor-pointer bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 shadow-sm"
              >
                <X className="w-3.5 h-3.5" />
                Cancel
              </button>
              <button
                onClick={handleSaveRefinements}
                disabled={saving || (draft && !localReceipt.timestamp)}
                type="button"
                className="inline-flex items-center gap-1 text-[11px] font-bold uppercase tracking-wider px-3 py-1.5 rounded-sm transition-all cursor-pointer bg-emerald-700 hover:bg-emerald-800 text-white shadow-sm"
              >
                <Save className="w-3.5 h-3.5" />
                {saving ? "Saving…" : draft ? "Confirm Bill" : "Save"}
              </button>
            </div>
          ) : (
            <div className="relative">
              <button
                type="button"
                title="More options"
                aria-label="More options"
                aria-haspopup="true"
                aria-expanded={menuOpen}
                onClick={() => setMenuOpen((open) => !open)}
                className="inline-flex items-center justify-center w-8 h-8 rounded-sm transition-all cursor-pointer bg-white hover:bg-slate-50 text-slate-500 border border-slate-300 shadow-sm"
              >
                <MoreVertical className="w-4 h-4" />
              </button>
              {menuOpen && (
                <div
                  className="absolute right-0 mt-1 w-32 bg-white border border-slate-300 rounded-sm shadow-lg z-10 overflow-hidden"
                  onClick={(e) => e.stopPropagation()}
                >
                  <button
                    type="button"
                    onClick={() => {
                      setMenuOpen(false);
                      setEditMode(true);
                    }}
                    className="w-full text-left px-3 py-2 text-[11px] font-bold uppercase tracking-wider text-slate-700 hover:bg-slate-100 transition cursor-pointer flex items-center gap-2"
                  >
                    <svg
                      className="w-3.5 h-3.5"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"
                      />
                    </svg>
                    Edit
                  </button>
                  <button
                    type="button"
                    onClick={handleRemoveTransaction}
                    className="w-full text-left px-3 py-2 text-[11px] font-bold uppercase tracking-wider text-rose-600 hover:bg-rose-50 transition cursor-pointer flex items-center gap-2"
                  >
                    <svg
                      className="w-3.5 h-3.5"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
                      />
                    </svg>
                    Remove
                  </button>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {draft && !localReceipt.timestamp && editMode && (
        <p role="alert" className="text-xs text-amber-800 bg-amber-50 border border-amber-200 px-3 py-2">Confirm a Timestamp before saving this Bill Draft.</p>
      )}
      {saveError && <p role="alert" className="text-xs text-red-700 bg-red-50 border border-red-200 px-3 py-2">{saveError}</p>}

      {/* Body: items table only. */}
      {expanded && (
        <div className="border border-t-0 border-slate-300 rounded-b-sm bg-white p-5">
          <div className="grid grid-cols-12 gap-2 text-[9px] font-bold uppercase tracking-wider text-slate-400 px-2 pb-2 border-b border-slate-200">
            <span className="col-span-4">Name</span>
            <span className={categoryColSpan}>Category / Subcategory</span>
            <span className="col-span-1 text-right">Price</span>
            <span className="col-span-1 text-right">Qty</span>
            <span className="col-span-1 text-right">Cost</span>
            {editMode && <span className="col-span-1" />}
          </div>

          <ul className="divide-y divide-slate-100">
            {localReceipt.items.map((item, index) => (
              <li
                key={index}
                className="grid grid-cols-12 items-center gap-2 py-2 px-2"
              >
                {editMode ? (
                  <>
                    <input
                      type="text"
                      value={item.name}
                      onChange={(e) =>
                        updateItem(index, { name: e.target.value })
                      }
                      aria-label={`Edit item ${index + 1} name`}
                      className="col-span-4 w-full px-2 py-1 text-xs bg-slate-50 border border-slate-300 rounded-sm focus:outline-none focus:border-zinc-900 transition"
                    />
                    <select
                      value={item.category ?? ""}
                      aria-label={`Edit item ${index + 1} category`}
                      onChange={(e) =>
                        updateItem(index, {
                          category: e.target.value || undefined,
                        })
                      }
                      className="col-span-2 w-full px-2 py-1 text-xs bg-slate-50 border border-slate-300 rounded-sm focus:outline-none focus:border-zinc-900 transition"
                    >
                      <option value="">—</option>
                      {categories.map((cat) => (
                        <option key={cat.name} value={cat.name}>
                          {cat.name}
                        </option>
                      ))}
                    </select>
                    {(() => {
                      const activeCategory =
                        item.category ??
                        categories[0]?.name ??
                        "";
                      const subcategories =
                        categories.find(
                          (cat) => cat.name === activeCategory,
                        )?.subcategories ?? [];
                      return (
                        <select
                          value={item.subcategory ?? ""}
                          aria-label={`Edit item ${index + 1} subcategory`}
                          onChange={(e) =>
                            updateItem(index, {
                              subcategory: e.target.value || undefined,
                            })
                          }
                          className="col-span-2 w-full px-2 py-1 text-xs bg-slate-50 border border-slate-300 rounded-sm focus:outline-none focus:border-zinc-900 transition"
                        >
                          <option value="">—</option>
                          {subcategories.map((sub) => (
                            <option key={sub} value={sub}>
                              {sub}
                            </option>
                          ))}
                        </select>
                      );
                    })()}
                    <input
                      type="number"
                      value={item.unitPrice}
                      onChange={(e) => {
                        const unit = parseFloat(e.target.value) || 0;
                        updateItem(index, {
                          unitPrice: unit,
                          totalPrice: unit * item.quantity,
                        });
                      }}
                      aria-label={`Edit item ${index + 1} price`}
                      className="col-span-1 w-full px-2 py-1 text-xs text-right bg-slate-50 border border-slate-300 rounded-sm focus:outline-none focus:border-zinc-900 transition tabular-nums"
                    />
                    <input
                      type="number"
                      value={item.quantity}
                      onChange={(e) => {
                        const qty = parseFloat(e.target.value) || 0;
                        updateItem(index, {
                          quantity: qty,
                          totalPrice: qty * item.unitPrice,
                        });
                      }}
                      aria-label={`Edit item ${index + 1} quantity`}
                      className="col-span-1 w-full px-2 py-1 text-xs text-right bg-slate-50 border border-slate-300 rounded-sm focus:outline-none focus:border-zinc-900 transition tabular-nums"
                    />
                    <span className="col-span-1 w-full tabular-nums text-right text-xs font-semibold text-emerald-700">
                      {item.totalPrice.toLocaleString()}
                    </span>
                    <button
                      onClick={() => removeLineItem(index)}
                      type="button"
                      title="Delete list entry"
                      aria-label={`Delete ${item.name} row`}
                      className="col-span-1 w-6 h-6 flex items-center justify-center rounded-sm bg-rose-50 text-rose-600 hover:bg-rose-100 transition cursor-pointer shrink-0"
                    >
                      <svg
                        className="w-3.5 h-3.5"
                        fill="none"
                        viewBox="0 0 24 24"
                        stroke="currentColor"
                      >
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          strokeWidth={2}
                          d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
                        />
                      </svg>
                    </button>
                  </>
                ) : (
                  <>
                    <span className="col-span-4 w-full text-xs font-bold text-zinc-900 truncate">
                      {item.name}
                    </span>
                    {/* Category / subcategory: allowed to stack onto a second
                        line when the cell has room (no hard truncate). Uses the
                        shared span so the numeric columns stay aligned with the
                        header. */}
                    <span
                      className={`${categoryColSpan} w-full text-xs text-slate-500 whitespace-normal leading-tight`}
                    >
                      {combineCategory(item.category, item.subcategory)}
                    </span>
                    <span className="col-span-1 w-full text-xs text-slate-500 tabular-nums text-right">
                      {item.unitPrice.toLocaleString()}
                    </span>
                    <span className="col-span-1 w-full text-xs text-slate-500 tabular-nums text-right">
                      {item.quantity}
                    </span>
                    <span className="col-span-1 w-full tabular-nums text-right text-xs font-semibold text-emerald-700">
                      {item.totalPrice.toLocaleString()}
                    </span>
                  </>
                )}
              </li>
            ))}
          </ul>

          {editMode && (
            <button
              onClick={addNewLine}
              type="button"
              className="mt-4 w-full text-[11px] font-medium py-2 rounded-sm border border-dashed border-slate-300 text-slate-500 hover:border-zinc-900 hover:text-zinc-900 transition cursor-pointer flex items-center justify-center gap-1"
            >
              <svg
                className="w-3.5 h-3.5"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M12 6v6m0 0v6m0-6h6m-6 0H6"
                />
              </svg>
              Add Item Row
            </button>
          )}
        </div>
      )}
    </div>
  );
}

/**
 * Combine an item's category and subcategory into one display cell, e.g.
 * "Food · Snacks", or "—" when neither is present.
 */
function combineCategory(
  category?: string,
  subcategory?: string,
): string {
  if (!category && !subcategory) return "—";
  return [category, subcategory].filter(Boolean).join(" · ");
}

/** Format an ISO timestamp for the transaction-row Date column (compact 24h). */
function formatReceiptDateTime(iso?: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (isNaN(d.getTime())) return iso;
  return d.toLocaleDateString(undefined, {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  });
}

/** Convert an ISO timestamp to a `datetime-local` input value. */
function toLocalInputValue(iso: string): string {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return "";
  return d.toISOString().slice(0, 16);
}
