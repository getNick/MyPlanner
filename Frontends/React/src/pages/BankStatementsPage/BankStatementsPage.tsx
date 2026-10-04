import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "@clerk/clerk-react";
import { useNavigate } from "react-router-dom";
import { AlertCircle, FileText, Landmark, Loader2, Upload } from "lucide-react";
import FinanceService from "../../services/FinanceService";
import TransactionList from "../../components/TransactionList/TransactionList";
import type { BankStatementImportResult, BankStatementSummary } from "../../types/bankImportTypes";
import {
  formatImportTargetLabel,
  isImportTarget,
  pickDefaultImportTarget,
  recalledImportTarget,
  rememberImportTarget,
} from "../../domain/paymentMethods";
import { statementRowsFor } from "../../domain/bankTransactions";
import type {
  BackendPaymentMethod,
} from "../../types/paymentMethodTypes";
import type { BackendTransaction, Category } from "../../types/receiptTypes";
import { transactionSaveBody } from "../../domain/transactionDetail";

/**
 * `/finance/bank` — statement import. One page, one question: which account is this file for?
 *
 * The picker lists only methods that can actually receive a statement (cards and savings accounts
 * whose Bank Provider is set), because importing into anything else would mean inventing columns for
 * a file that has none. Choosing is remembered, so the daily case — same card, new month — costs one
 * click. Methods are managed on `/finance/payment-methods`, reached from the header; nothing is edited here.
 *
 * Import is two steps over one file: picking it previews what the statement says (row count, date
 * span, rows needing review) and nothing is stored until the page confirms. The browser keeps the
 * file between the calls; the server stays stateless. Every refusal — a PDF, a bank we cannot read,
 * a target with no bank set — comes back as its reason, not a shrug.
 *
 * Below the import sits the account's own Bank Transactions, newest-first, read from the same
 * `GET /finance/transactions` every other ledger view uses. "Imported 41 rows" is a claim; the list
 * is the check. Rows are scoped to the chosen account on purpose — the page just asked which account
 * the file was for, and answering about a different one would be worse than answering nothing.
 *
 * Visually this page is a twin of `/finance/shopping-bills`: the same dashed dropzone (icon tile, bold
 * instruction, format chip), the same refusal card beneath it, and the same white card-of-rows shell
 * with an uppercase header bar and one bordered slate row per ledger entry. Keep the two in step.
 */

function formatStatementDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    day: "2-digit",
    month: "short",
    year: "numeric",
  });
}

export const BankStatementsPage: React.FC = () => {
  const { getToken } = useAuth();
  const navigate = useNavigate();

  // Stable identity: the finance client is memoized on this, and a fresh closure every render would
  // rebuild the client and re-read the list.
  const accessTokenRef = useRef(() => getToken({ template: "AspNetToken" }));
  accessTokenRef.current = () => getToken({ template: "AspNetToken" });
  const financeService = useMemo(() => new FinanceService(() => accessTokenRef.current()), []);

  const [methods, setMethods] = useState<BackendPaymentMethod[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [targetId, setTargetId] = useState<string | null>(null);

  // The two-step import: a picked file waits as a preview until the page confirms it.
  const [pendingFile, setPendingFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<BankStatementSummary | null>(null);
  const [importResult, setImportResult] = useState<BankStatementImportResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [isDragOver, setIsDragOver] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const load = useCallback(async () => {
    try {
      const list = await financeService.getPaymentMethods();
      setMethods(list);
      setLoadError(null);
      setTargetId(pickDefaultImportTarget(list, recalledImportTarget())?.id ?? null);
    } catch (error) {
      console.error("Failed to load payment methods:", error);
      // An unread list is not an empty one: saying so keeps the user from going off to build cards
      // they already have.
      setLoadError("Could not load payment methods. Check that the API is reachable.");
    } finally {
      setLoading(false);
    }
  }, [financeService]);

  useEffect(() => {
    void load();
    void (async () => {
      try { setCategories(await financeService.getReceiptCategories()); }
      catch { /* Backend still validates classifications if loading fails. */ }
    })();
  }, [load, financeService]);

  const targets = useMemo(() => methods.filter(isImportTarget), [methods]);
  // The account the ledger below is about: the same one the picker names, never a different one.
  const chosenTarget = targets.find((method) => method.id === targetId) ?? null;

  // The ledger as read, not as filtered: one read answers for every account the picker can name, so
  // switching accounts re-filters locally instead of re-fetching.
  const [ledger, setLedger] = useState<BackendTransaction[]>([]);
  const [ledgerError, setLedgerError] = useState<string | null>(null);
  const [loadingLedger, setLoadingLedger] = useState(true);

  const statementRows = useMemo(() => statementRowsFor(ledger, targetId), [ledger, targetId]);

  // A failed read is kept apart from an empty one: after an import, "nothing there yet" and "could
  // not read" are different answers and only one is true.
  const loadLedger = useCallback(async () => {
    try {
      setLedger(await financeService.getTransactions());
      setLedgerError(null);
    } catch (error) {
      console.error("Failed to load bank transactions:", error);
      setLedgerError(
        "Could not read the ledger, so imported rows may be missing below. Nothing was lost — reload to try again."
      );
    } finally {
      setLoadingLedger(false);
    }
  }, [financeService]);

  useEffect(() => {
    void loadLedger();
  }, [loadLedger]);

  const openPaymentMethods = () => navigate("/finance/payment-methods");

  // Picking a file previews it: the answer arrives before anything is written down.
  const handleFilePicked = async (file: File | null) => {
    if (!file || !targetId) return;
    setBusy(true);
    setActionError(null);
    setImportResult(null);
    setPreview(null);
    setPendingFile(null);
    try {
      const summary = await financeService.previewBankingFile(targetId, file);
      setPendingFile(file);
      setPreview(summary);
    } catch (error) {
      // The server refused the file or the target; its reason is the whole message.
      setActionError(error instanceof Error ? error.message : String(error));
    } finally {
      setBusy(false);
    }
  };

  // Both doors — picker and drop — go through here, so a dropped PDF is refused with its reason
  // instead of silently opening a preview of nothing.
  const handleStatementFile = async (file: File | null) => {
    if (!file) return;
    if (!file.name.toLowerCase().endsWith(".csv") && file.type !== "text/csv") {
      setActionError("Please choose a statement export in CSV format.");
      return;
    }
    await handleFilePicked(file);
  };

  const handleConfirmImport = async () => {
    if (!pendingFile || !targetId) return;
    setBusy(true);
    setActionError(null);
    try {
      const result = await financeService.importBankingFile(targetId, pendingFile);
      setImportResult(result);
      // The counts above are the claim; the rows below are the proof, so the read happens now.
      await loadLedger();
    } catch (error) {
      setActionError(error instanceof Error ? error.message : String(error));
    } finally {
      setPendingFile(null);
      setPreview(null);
      setBusy(false);
    }
  };

  const handleCancelPreview = () => {
    setPendingFile(null);
    setPreview(null);
  };

  return (
    // Same page frame and centered column as `/finance/shopping-bills`, so the two pages read as one
    // design at any window size: full-bleed slate background with a `max-w-4xl` column centered in it.
    <div className="min-h-screen bg-slate-50 font-mono">
      <div className="max-w-4xl mx-auto w-full px-8 py-8 space-y-6">
        <div className="flex items-start justify-between gap-4 border-b border-slate-200 pb-4">
          <div>
            <div className="flex items-center space-x-2 text-xs text-slate-500 uppercase tracking-widest">
              <Landmark className="w-3.5 h-3.5 text-zinc-900" />
              <span>Module // Finance · Bank</span>
            </div>
            <h1 className="text-2xl font-bold text-zinc-900 tracking-tight mt-0.5 uppercase">
              Statement import
            </h1>
            <p className="text-xs text-slate-500 mt-1 max-w-2xl">
              Pick the account a statement belongs to, then hand it the file. Import reads that
              account's bank format, so only methods with a known bank appear below.
            </p>
          </div>

          <button
            type="button"
            onClick={openPaymentMethods}
            className="bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 px-3 py-1.5 rounded-sm text-xs font-bold whitespace-nowrap"
          >
            Payment methods
          </button>
        </div>

        {loadError && (
          <p className="text-xs font-semibold text-red-700 bg-red-50 border border-red-200 rounded-sm px-3 py-2">
            {loadError}
          </p>
        )}

        {loading ? (
          <div className="flex items-center justify-center gap-2 py-16 text-xs text-slate-500 uppercase tracking-widest">
            <Loader2 className="w-4 h-4 animate-spin" />
            <span>Loading…</span>
          </div>
        ) : loadError ? (
          // The banner above is the whole story. Showing "you have nothing to import into" as well
          // would blame the household's data for an API that did not answer.
          null
        ) : targets.length === 0 ? (
          // Not "import a statement" — there is nothing valid to import into. Point at the page that
          // can fix it rather than at a picker full of methods that would be refused.
          <div className="bg-white border border-slate-300 rounded-sm px-5 py-16 text-center space-y-4">
            <p className="text-sm font-bold text-zinc-900">No account can receive a statement</p>
            <p className="text-xs text-slate-500 max-w-md mx-auto">
              Nothing here can receive a statement yet. Import needs a bank card or savings account with
              its bank set on it — cash wallets never have a statement, and a card with no bank has no
              format to read.
            </p>
            <button
              type="button"
              onClick={openPaymentMethods}
              className="bg-zinc-900 hover:bg-zinc-800 text-white px-3 py-1.5 rounded-sm text-xs font-bold"
            >
              Add a payment method
            </button>
          </div>
        ) : (
          <div className="space-y-4">
            <div className="max-w-md">
              <label
                htmlFor="bank-import-target"
                className="block text-[10px] uppercase tracking-widest text-slate-500 mb-1"
              >
                Import into
              </label>
              <select
                id="bank-import-target"
                value={targetId ?? ""}
                onChange={(event) => {
                  setTargetId(event.target.value);
                  rememberImportTarget(event.target.value);
                }}
                className="bg-white border border-slate-300 rounded-sm px-2 py-1.5 text-xs text-zinc-900 w-full focus:outline-none focus:border-zinc-900"
              >
                {targets.map((method) => (
                  <option key={method.id} value={method.id}>
                    {formatImportTargetLabel(method)}
                  </option>
                ))}
              </select>
            </div>

            {importResult && (
              <p className="text-xs text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-sm px-3 py-2">
                Imported {importResult.insertedRowCount} row
                {importResult.insertedRowCount === 1 ? "" : "s"} into{" "}
                {importResult.summary.paymentMethodName}.
                {importResult.duplicateRowCount > 0 &&
                  ` ${importResult.duplicateRowCount} already in the ledger were skipped.`}
              </p>
            )}

            {preview ? (
              // The confirmation step: what the file says, into which account, before it is written down.
              <div
                aria-label="Statement preview"
                className="bg-white border border-slate-300 rounded-sm overflow-hidden"
              >
                <div className="flex items-center justify-between gap-4 border-b border-slate-200 px-5 py-3">
                  <span className="text-xs font-bold uppercase tracking-wider text-zinc-900">
                    Statement preview
                  </span>
                  <span className="text-[10px] text-slate-500 whitespace-nowrap">
                    NOT STORED YET · CONFIRM TO IMPORT
                  </span>
                </div>
                <div className="px-5 py-4 space-y-3">
                  <p className="text-sm font-bold text-zinc-900 uppercase tracking-tight">
                    {pendingFile?.name} — {preview.rowCount} rows
                    {preview.firstTimestamp && preview.lastTimestamp
                      ? ` from ${formatStatementDate(preview.firstTimestamp)} to ${formatStatementDate(preview.lastTimestamp)}`
                      : ""}
                  </p>
                  <p className="text-xs text-slate-600">
                    Import into{" "}
                    {(() => {
                      const target = targets.find((method) => method.id === targetId);
                      return target
                        ? formatImportTargetLabel(target)
                        : `${preview.paymentMethodName} — ${preview.bankProvider}`;
                    })()}
                  </p>
                  {preview.needsReview.length > 0 && (
                    <ul className="text-xs text-amber-800 bg-amber-50 border border-amber-200 rounded-sm px-3 py-2 space-y-1">
                      {preview.needsReview.map((row) => (
                        <li key={row.rowNumber}>
                          Line {row.rowNumber}
                          {row.description ? ` (${row.description})` : ""}: {row.reason}
                        </li>
                      ))}
                    </ul>
                  )}
                  <div className="flex space-x-2 pt-1">
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => void handleConfirmImport()}
                      className="bg-zinc-900 hover:bg-zinc-800 disabled:opacity-50 text-white px-3 py-1.5 rounded-sm text-xs font-bold"
                    >
                      {busy ? "Importing…" : "Confirm import"}
                    </button>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={handleCancelPreview}
                      className="bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 px-3 py-1.5 rounded-sm text-xs font-bold"
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              </div>
            ) : (
              // Same dropzone language as the receipt scanner: dashed border that answers a drag,
              // a black icon tile, one bold instruction, and the format note beneath it.
              <div
                className="w-full"
                onDragOver={(event) => {
                  event.preventDefault();
                  setIsDragOver(true);
                }}
                onDragLeave={() => setIsDragOver(false)}
                onDrop={(event) => {
                  event.preventDefault();
                  setIsDragOver(false);
                  void handleStatementFile(event.dataTransfer.files?.[0] ?? null);
                }}
              >
                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".csv,text/csv"
                  aria-label="Statement file"
                  className="hidden"
                  onChange={(event) => {
                    void handleStatementFile(event.target.files?.[0] ?? null);
                    // Allow re-picking the same file — e.g. after a refusal worth reading twice.
                    event.target.value = "";
                  }}
                />
                <button
                  type="button"
                  aria-label="Statement upload"
                  disabled={busy}
                  onClick={() => fileInputRef.current?.click()}
                  className={`relative flex w-full flex-col items-center justify-center text-center min-h-[220px] rounded-sm border-2 border-dashed p-8 transition-all ${
                    isDragOver
                      ? "border-zinc-800 bg-slate-50"
                      : busy
                        ? "border-slate-200 bg-slate-50/50 cursor-not-allowed"
                        : "border-slate-300 hover:border-zinc-800 bg-white hover:bg-slate-50"
                  }`}
                >
                  {busy ? (
                    <>
                      <Loader2 className="w-6 h-6 animate-spin text-zinc-900" />
                      <span className="mt-4 text-sm font-bold text-slate-900">
                        Reading statement…
                      </span>
                      <span className="mt-1 text-xs text-slate-500 max-w-[280px] animate-pulse">
                        Counting rows and checking dates before anything is stored
                      </span>
                    </>
                  ) : (
                    <>
                      <span className="p-3 bg-zinc-900 rounded-sm mb-4 text-white flex items-center justify-center">
                        <Upload className="w-6 h-6" />
                      </span>
                      <span className="text-sm font-bold text-slate-900">
                        Upload your Bank Statement CSV
                      </span>
                      <span className="mt-1 text-xs text-slate-500 max-w-[280px]">
                        Drag and drop your statement export or click to browse files
                      </span>
                      <span className="mt-4 inline-flex items-center gap-1.5 px-2.5 py-1 bg-slate-100 rounded-sm text-[10px] text-slate-600">
                        <FileText className="w-3.5 h-3.5" />
                        <span>CSV export from your bank — previewed before anything is stored</span>
                      </span>
                    </>
                  )}
                </button>
              </div>
            )}

            {actionError && (
              <div className="p-3 bg-rose-50 border border-rose-100 rounded-sm flex items-start gap-2.5 text-xs text-rose-700">
                <AlertCircle className="w-4 h-4 text-rose-500 shrink-0 mt-0.5" />
                <div className="flex-1">
                  <span className="font-medium">Import Failed</span>
                  <p className="mt-0.5 text-rose-600">{actionError}</p>
                </div>
              </div>
            )}

            {chosenTarget && (
              // The ledger card, in the same shape as the processed-receipts card: white shell,
              // uppercase header bar with the count, then one bordered card per row.
              <section
                aria-label="Bank transactions"
                className="bg-white border border-slate-300 rounded-sm overflow-hidden"
              >
                <div className="flex items-center justify-between gap-4 border-b border-slate-200 px-5 py-3">
                  <h2 className="text-xs font-bold uppercase tracking-wider text-zinc-900">
                    Bank transactions — {formatImportTargetLabel(chosenTarget)} ({statementRows.length})
                  </h2>
                  <span className="text-[10px] text-slate-500 whitespace-nowrap shrink-0">
                    REAL DATA · LEDGER-SYNCED
                  </span>
                </div>

                {loadingLedger ? (
                  <div className="flex items-center justify-center gap-2 px-5 py-16 text-xs text-slate-500">
                    <Loader2 className="w-4 h-4 animate-spin" /> Reading the ledger…
                  </div>
                ) : ledgerError ? (
                  <div className="px-5 py-16 text-center">
                    <p className="text-sm font-bold text-zinc-900 mb-1">Ledger not read</p>
                    <p className="text-xs text-slate-500 max-w-xs mx-auto">{ledgerError}</p>
                  </div>
                ) : statementRows.length === 0 ? (
                  <div className="px-5 py-16 text-center">
                    <p className="text-sm font-bold text-zinc-900 mb-1">No statement rows yet</p>
                    <p className="text-xs text-slate-500 max-w-xs mx-auto">
                      No statement rows for {chosenTarget.name} yet — import a CSV above.
                    </p>
                  </div>
                ) : (
                  <TransactionList
                    transactions={statementRows}
                    label="Bank transactions"
                    paymentMethodName={id => methods.find(method => method.id === id)?.name || null}
                    categories={categories}
                    onSave={async (row, detail) => {
                      const saved = await financeService.updateTransaction(transactionSaveBody(row, detail));
                      if (!saved) throw new Error("Transaction no longer exists.");
                      setLedger(previous => previous.map(transaction => transaction.id === saved.id ? saved : transaction));
                      await loadLedger();
                    }}
                    paymentMethodCurrency={id => methods.find(method => method.id === id)?.currency || null}
                  />
                )}
              </section>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

export default BankStatementsPage;
