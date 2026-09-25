import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { CreditCard, Landmark, Pencil, PiggyBank, Plus, Trash2, Wallet } from "lucide-react";
import FinanceService from "../../services/FinanceService";
import {
  compareForListOrder,
  providerFromChoice,
  providerOptions,
  supportsBankStatement,
} from "../../domain/paymentMethods";
import type {
  BackendCurrency,
  BackendPaymentMethod,
  BackendPaymentMethodType,
} from "../../types/paymentMethodTypes";

/**
 * The payment-methods list and form, on its own route (`/finance/payment-methods`) reached from `/finance/bank`.
 * It is not in the sidebar: it is where you fix the methods, not somewhere you go to look at money.
 *
 * Three rules come from the ledger, not from taste:
 *  - Nothing is created behind the user. A Cash Wallet exists because someone made one here; no
 *    load-time call mints a row (`/finance/bank` asks instead of assuming — see ticket 06).
 *  - Deleting a method that already carries transactions is refused by the backend, in either
 *    direction. The refusal is reported with the server's count and the row stays on screen.
 *    A Cash Wallet is deletable like anything else: a wallet you made by hand and could not remove
 *    would be indefensible.
 *  - Bank Provider is a closed choice offered only to methods that can hold a statement (cards,
 *    savings accounts). The form blocks Save until one is picked — including `Other / not listed`,
 *    which stores null — so "this card has no format" is always a decision, never an oversight.
 */

const TYPE_OPTIONS: { value: BackendPaymentMethodType; label: string }[] = [
  { value: "BankCard", label: "Bank card" },
  { value: "SavingsAccount", label: "Savings account" },
  { value: "Cash", label: "Cash wallet" },
  { value: "Other", label: "Other" },
];

const CURRENCY_OPTIONS: { value: BackendCurrency; label: string }[] = [
  { value: "UAH", label: "UAH" },
  { value: "USD", label: "USD" },
  { value: "EURO", label: "EUR" },
];

const TYPE_LABELS: Record<BackendPaymentMethodType, string> = {
  BankCard: "Bank card",
  Cash: "Cash",
  SavingsAccount: "Savings account",
  Other: "Other",
};

function typeIcon(method: BackendPaymentMethod) {
  const className = "w-4 h-4 text-slate-600";
  switch (method.type) {
    case "BankCard":
      return <CreditCard className={className} />;
    case "Cash":
      return <Wallet className={className} />;
    case "SavingsAccount":
      return <PiggyBank className={className} />;
    default:
      return <Landmark className={className} />;
  }
}

// A refusal is data, never an exception: say what is in the way using the
// server's count when it supplied one.
function refusalMessage(transactionCount: number | null, explanation: string | null): string {
  if (explanation) return explanation;
  if (transactionCount === null) {
    return "Transactions are recorded on this payment method. Delete or re-attribute them first.";
  }
  return `${transactionCount} transaction${transactionCount === 1 ? "" : "s"} recorded on this payment method. Delete or re-attribute them first.`;
}

interface Draft {
  id?: string;
  name: string;
  type: BackendPaymentMethodType;
  currency: BackendCurrency;
  bankProvider: string;
}

const EMPTY_DRAFT: Draft = { name: "", type: "BankCard", currency: "UAH", bankProvider: "" };

interface PaymentMethodsPanelProps {
  /** Injected so the panel is testable without an auth provider mounted. */
  getToken: () => Promise<string | null>;
}

export const PaymentMethodsPanel: React.FC<PaymentMethodsPanelProps> = ({ getToken }) => {
  // Read through a ref so the client (and therefore the initial load) survives a
  // caller that passes a fresh getToken closure on every render.
  const getTokenRef = useRef(getToken);
  getTokenRef.current = getToken;
  const financeService = useMemo(() => new FinanceService(() => getTokenRef.current()), []);

  const [methods, setMethods] = useState<BackendPaymentMethod[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // null = form closed; otherwise a new draft (no id) or an edit (with id).
  const [draft, setDraft] = useState<Draft | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // Row awaiting the second click that actually deletes.
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      // A pure read: this screen never creates a row on your behalf.
      const list = await financeService.getPaymentMethods();
      setMethods(list);
      setLoadError(null);
    } catch (error) {
      console.error("Failed to load payment methods:", error);
      setLoadError("Could not load payment methods. Check that the API is reachable.");
    } finally {
      setLoading(false);
    }
  }, [financeService]);

  useEffect(() => {
    void load();
  }, [load]);

  const openCreateForm = () => {
    setDraft({ ...EMPTY_DRAFT });
    setFormError(null);
    setNotice(null);
  };

  const openEditForm = (method: BackendPaymentMethod) => {
    setPendingDeleteId(null);
    setNotice(null);
    setDraft({
      id: method.id,
      name: method.name,
      type: method.type,
      currency: method.currency,
      bankProvider: method.bankProvider ?? "",
    });
    setFormError(null);
  };

  const closeForm = () => {
    setDraft(null);
    setFormError(null);
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!draft) return;

    const name = draft.name.trim();
    if (!name) {
      setFormError("Name is required.");
      return;
    }

    // Required to the user, optional to the API: an unanswered provider never leaves this form, and
    // a method that cannot hold a statement posts no provider at all.
    const bankBacked = supportsBankStatement(draft.type);
    if (bankBacked && !draft.bankProvider) {
      setFormError(
        "Bank provider is required — pick the bank that issued it, or Other / not listed."
      );
      return;
    }

    const body = {
      name,
      type: draft.type,
      currency: draft.currency,
      bankProvider: bankBacked ? providerFromChoice(draft.bankProvider) : null,
    };

    try {
      setSaving(true);
      setFormError(null);
      if (draft.id) {
        const updated = await financeService.updatePaymentMethod({ id: draft.id, ...body });
        if (!updated) {
          setFormError("That payment method no longer exists. Reload the list.");
          return;
        }
      } else {
        await financeService.createPaymentMethod(body);
      }
      closeForm();
      await load();
    } catch (error) {
      setFormError(error instanceof Error ? error.message : "Could not save the payment method.");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (method: BackendPaymentMethod) => {
    setPendingDeleteId(null);
    setNotice(null);
    try {
      const result = await financeService.deletePaymentMethod(method.id);
      if (result.status === "deleted") {
        setNotice(`Removed ${method.name}.`);
        await load();
        return;
      }
      if (result.status === "in-use") {
        setNotice(refusalMessage(result.transactionCount, result.explanation));
        return;
      }
      setNotice(`${method.name} was already gone.`);
      await load();
    } catch (error) {
      console.error(`Failed to delete payment method ${method.id}:`, error);
      setNotice("Could not delete that payment method.");
    }
  };

  const inputClass =
    "bg-white border border-slate-300 rounded-sm px-2 py-1.5 text-xs text-zinc-900 focus:outline-none focus:border-zinc-900";
  const labelClass = "block text-[10px] uppercase tracking-widest text-slate-500 mb-1";

  const sortedMethods = [...methods].sort(compareForListOrder);
  // The row being edited, so its stored provider stays offerable even if it fell out of the
  // supported list — editing a name must not quietly rewrite which bank the row claims.
  const editing = draft?.id ? methods.find((method) => method.id === draft.id) : undefined;

  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between border-b border-slate-200 pb-2">
        <h2 className="text-xs font-bold uppercase tracking-widest text-zinc-900">
          Payment methods
        </h2>
        <button
          type="button"
          onClick={openCreateForm}
          className="bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 px-3 py-1.5 rounded-sm text-xs font-bold flex items-center space-x-1.5"
        >
          <Plus className="w-3.5 h-3.5 text-zinc-800" />
          <span>Add payment method</span>
        </button>
      </div>

      {loadError && (
        <p className="text-xs text-red-700 bg-red-50 border border-red-200 rounded-sm px-3 py-2">
          {loadError}
        </p>
      )}

      {loading ? (
        <p className="text-xs text-slate-500 uppercase tracking-widest">Loading…</p>
      ) : (
        <ul className="border border-slate-300 rounded-sm divide-y divide-slate-200 bg-white">
          {sortedMethods.map((method) => (
            <li
              key={method.id}
                className="flex flex-wrap items-center gap-3 px-4 py-3"
            >
              <span className="flex items-center space-x-2 min-w-[12rem] flex-1">
                {typeIcon(method)}
                <span className="text-sm font-bold text-zinc-900">{method.name}</span>
              </span>

              <span className="text-xs uppercase tracking-widest text-slate-500 w-32">
                {TYPE_LABELS[method.type]}
              </span>
              <span className="text-xs text-slate-600 w-24">{method.currency}</span>
              {/* A card or savings account with no bank is listed but unusable for import; saying so
                  in the row is what keeps "no format" visible without a badge or filter. */}
              <span
                className="text-xs text-slate-600 w-28 truncate"
                title={
                  method.bankProvider || !supportsBankStatement(method.type)
                    ? undefined
                    : "Nothing can import a statement for this method until a bank is set here."
                }
              >
                {method.bankProvider ??
                  (supportsBankStatement(method.type) ? "no statement format" : "—")}
              </span>

              <span className="flex items-center space-x-2 ml-auto">
                <button
                  type="button"
                  onClick={() => openEditForm(method)}
                  aria-label={`Edit ${method.name}`}
                  title={`Edit ${method.name}`}
                  className="text-slate-500 hover:text-zinc-900 border border-slate-300 rounded-sm p-1.5 hover:bg-slate-50"
                >
                  <Pencil className="w-3.5 h-3.5" />
                </button>

                {pendingDeleteId === method.id ? (
                  <>
                    <button
                      type="button"
                      onClick={() => setPendingDeleteId(null)}
                      className="text-xs font-bold text-slate-600 border border-slate-300 rounded-sm px-2 py-1 hover:bg-slate-50"
                    >
                      Cancel
                    </button>
                    <button
                      type="button"
                      onClick={() => handleDelete(method)}
                      className="text-xs font-bold text-red-700 border border-red-300 rounded-sm px-2 py-1 bg-white hover:bg-red-50"
                    >
                      Confirm delete
                    </button>
                  </>
                ) : (
                  <button
                    type="button"
                    onClick={() => setPendingDeleteId(method.id)}
                    aria-label={`Delete ${method.name}`}
                    title={`Delete ${method.name}`}
                    className="text-slate-500 hover:text-red-700 border border-slate-300 rounded-sm p-1.5 hover:bg-slate-50"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}

      {notice && (
        <p className="text-xs text-amber-800 bg-amber-50 border border-amber-200 rounded-sm px-3 py-2">
          {notice}
        </p>
      )}

      {draft && (
        <form
          onSubmit={handleSubmit}
          className="border border-slate-300 rounded-sm bg-white p-4 space-y-3"
        >
          <h3 className="text-xs font-bold uppercase tracking-widest text-zinc-900">
            {draft.id ? "Edit payment method" : "New payment method"}
          </h3>

          <div className="grid grid-cols-1 sm:grid-cols-4 gap-3">
            <div>
              <label htmlFor="payment-method-name" className={labelClass}>
                Name
              </label>
              <input
                id="payment-method-name"
                value={draft.name}
                onChange={(e) => setDraft({ ...draft, name: e.target.value })}
                placeholder="Monobank black"
                className={`${inputClass} w-full`}
              />
            </div>

            <div>
              <label htmlFor="payment-method-type" className={labelClass}>
                Type
              </label>
              <select
                id="payment-method-type"
                value={draft.type}
                onChange={(e) =>
                  setDraft({ ...draft, type: e.target.value as BackendPaymentMethodType })
                }
                className={inputClass}
              >
                {TYPE_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label htmlFor="payment-method-currency" className={labelClass}>
                Currency
              </label>
              <select
                id="payment-method-currency"
                value={draft.currency}
                onChange={(e) =>
                  setDraft({ ...draft, currency: e.target.value as BackendCurrency })
                }
                className={inputClass}
              >
                {CURRENCY_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>

            {supportsBankStatement(draft.type) && (
              // Only where the answer exists: a cash wallet has no issuing bank. The list is closed
              // because every entry promises a statement format ticket 02 can actually parse.
              <div>
                <label htmlFor="payment-method-provider" className={labelClass}>
                  Bank provider
                </label>
                <select
                  id="payment-method-provider"
                  value={draft.bankProvider}
                  onChange={(e) => setDraft({ ...draft, bankProvider: e.target.value })}
                  className={`${inputClass} w-full`}
                >
                  {/* Nothing chosen yet — that is what lets Save refuse an unanswered row. */}
                  <option value="">Choose a bank…</option>
                    {providerOptions(editing?.bankProvider).map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>

          {formError && <p className="text-xs text-red-700">{formError}</p>}

          <div className="flex items-center space-x-2">
            <button
              type="submit"
              disabled={saving}
              className="bg-zinc-900 hover:bg-zinc-800 text-white px-3 py-1.5 rounded-sm text-xs font-bold disabled:opacity-50"
            >
              {saving ? "Saving…" : draft.id ? "Save changes" : "Create payment method"}
            </button>
            <button
              type="button"
              onClick={closeForm}
              className="bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 px-3 py-1.5 rounded-sm text-xs font-bold"
            >
              Cancel
            </button>
          </div>
        </form>
      )}
    </section>
  );
};

export default PaymentMethodsPanel;
