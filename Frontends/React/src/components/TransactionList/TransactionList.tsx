import React, { useState } from "react";
import { ChevronDown, Landmark, Receipt } from "lucide-react";
import { displayCurrency } from "../../domain/paymentMethods";
import { provisionalBillAgeDays } from "../../domain/reconciliation";
import type { BackendTransaction } from "../../types/receiptTypes";

interface TransactionListProps {
  transactions: BackendTransaction[];
  label: string;
  emptyMessage?: string;
  paymentMethodName?: (id: string | null) => string | null;
  renderAmountDetail?: (transaction: BackendTransaction) => React.ReactNode;
  onDelete?: (transaction: BackendTransaction) => void;
  renderRow?: (transaction: BackendTransaction) => React.ReactNode;
}

const number = new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const moneyDelta = (value: number) => number.format(value);

/** Shared, presentational transaction list. Selection, loading, and mutations belong to callers. */
export default function TransactionList({
  transactions,
  label,
  emptyMessage = "No transactions yet.",
  paymentMethodName,
  renderAmountDetail,
  onDelete,
  renderRow,
}: TransactionListProps) {
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});

  return (
    <ul aria-label={label} className="flex flex-col gap-3 p-4">
      {transactions.length === 0 ? (
        <li className="px-5 py-12 text-center text-xs text-slate-500">{emptyMessage}</li>
      ) : transactions.map((transaction) => {
        if (renderRow) return <li key={transaction.id}>{renderRow(transaction)}</li>;
        const canExpand = transaction.items.length > 0 && (transaction.dataOrigin === "Receipt" || transaction.dataOrigin === "Reconciled");
        const isExpanded = expanded[transaction.id] ?? false;
        const age = transaction.dataOrigin === "Receipt" ? provisionalBillAgeDays(transaction) : null;
        const method = paymentMethodName?.(transaction.paymentMethodId);
        return (
          <li key={transaction.id} className="bg-slate-50 border border-slate-300 rounded-sm px-5 py-4">
            <div className="flex items-center justify-between gap-4">
              <div className="flex items-center gap-3 min-w-0 flex-1">
                {transaction.dataOrigin === "Bank" ? <Landmark className="w-4 h-4 text-slate-500 shrink-0" /> : <Receipt className="w-4 h-4 text-slate-500 shrink-0" />}
                <div className="min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    {canExpand ? (
                      <button type="button" aria-expanded={isExpanded} aria-controls={`items-${transaction.id}`} onClick={() => setExpanded((previous) => ({ ...previous, [transaction.id]: !isExpanded }))} className="text-sm font-bold text-zinc-900 uppercase tracking-tight truncate text-left hover:underline">
                        {transaction.description || "Unnamed transaction"}<ChevronDown className={`w-3.5 h-3.5 inline-block ml-1 transition-transform ${isExpanded ? "rotate-180" : ""}`} />
                      </button>
                    ) : <span className="text-sm font-bold text-zinc-900 uppercase tracking-tight truncate">{transaction.description || "Unnamed transaction"}</span>}
                    {transaction.dataOrigin === "Reconciled" && <span className="px-2 py-0.5 rounded-sm bg-emerald-100 text-emerald-700 text-[9px] font-bold uppercase tracking-wider">Reconciled</span>}
                    {transaction.dataOrigin === "Receipt" && <span className="px-2 py-0.5 rounded-sm bg-amber-100 text-amber-700 text-[9px] font-bold uppercase tracking-wider">Provisional</span>}
                    {transaction.dataOrigin === "Manual" && <span className="px-2 py-0.5 rounded-sm bg-slate-200 text-slate-700 text-[9px] font-bold uppercase tracking-wider">Manual</span>}
                  </div>
                  <div className="text-xs text-slate-500 mt-1">
                    {transaction.timestamp ? new Date(transaction.timestamp).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" }) : "No date"}
                    {method ? ` · ${method}` : ""}
                    {transaction.dataOrigin === "Bank" ? " · Bank" : transaction.dataOrigin === "Receipt" ? " · Receipt" : transaction.dataOrigin === "Manual" ? " · Manual" : ""}
                  </div>
                  {age !== null && <div className="text-[10px] font-semibold text-amber-700 mt-1">No bank match · {age} days</div>}
                </div>
              </div>
              <div className="shrink-0 text-right">
                <span className="text-sm font-bold text-zinc-900 tabular-nums">{transaction.type === "Expense" ? "−" : transaction.type === "Income" ? "+" : ""}{number.format(transaction.amount)} <span className="text-[10px] text-slate-500">{displayCurrency(transaction.currency)}</span></span>
                {renderAmountDetail?.(transaction)}
                {onDelete && <button type="button" onClick={() => onDelete(transaction)} className="block mt-1 text-[10px] text-slate-500 hover:text-rose-700">Delete</button>}
                {transaction.dataOrigin === "Reconciled" && transaction.moneyDelta !== null && <span className="block mt-1 text-[10px] font-semibold text-amber-700 tabular-nums">Money Delta · {moneyDelta(transaction.moneyDelta)} {displayCurrency(transaction.currency)}</span>}
              </div>
            </div>
            {canExpand && <ul id={`items-${transaction.id}`} aria-label={`Line Items for ${transaction.description}`} hidden={!isExpanded} className="mt-3 border-t border-slate-200 pt-2 space-y-1">{transaction.items.map((item) => <li key={item.id} className="flex justify-between gap-3 text-xs text-slate-600"><span>{item.quantity}× {item.name}</span><span className="tabular-nums">{number.format(item.totalPrice)} {displayCurrency(transaction.currency)}</span></li>)}</ul>}
          </li>
        );
      })}
    </ul>
  );
}
