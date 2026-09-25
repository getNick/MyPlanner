import React, { useCallback, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "@clerk/clerk-react";
import { ArrowLeft, CreditCard } from "lucide-react";
import PaymentMethodsPanel from "../../components/PaymentMethods/PaymentMethodsPanel";

/**
 * `/finance/payment-methods` — the household's cards, savings accounts and cash wallets.
 *
 * It is a page rather than a sidebar destination on purpose: you come here to fix a method (set its
 * bank, retire an old card) and then go back to importing. Keeping it off the sidebar also keeps
 * `/finance/bank` a single question instead of a management screen with an upload box bolted on.
 */
export const PaymentMethodsPage: React.FC = () => {
  const { getToken } = useAuth();
  const navigate = useNavigate();

  // Stable identity: the panel memoizes its finance client on this callback.
  const accessTokenRef = useRef(() => getToken({ template: "AspNetToken" }));
  accessTokenRef.current = () => getToken({ template: "AspNetToken" });
  const accessToken = useCallback(() => accessTokenRef.current(), []);

  return (
    <div className="space-y-6 font-mono">
      <div className="border-b border-slate-200 pb-4">
        <button
          type="button"
          onClick={() => navigate("/finance/bank")}
          className="flex items-center space-x-1.5 text-xs text-slate-500 uppercase tracking-widest hover:text-zinc-900"
        >
          <ArrowLeft className="w-3.5 h-3.5" />
          <span>Back to bank</span>
        </button>
        <div className="flex items-center space-x-2 text-xs text-slate-500 uppercase tracking-widest mt-3">
          <CreditCard className="w-3.5 h-3.5 text-zinc-900" />
          <span>Module // Finance · Payment methods</span>
        </div>
        <h1 className="text-2xl font-bold text-zinc-900 tracking-tight mt-0.5 uppercase">
          Payment methods
        </h1>
        <p className="text-xs text-slate-500 mt-1 max-w-2xl">
          Every account money moves through, and the cash wallets it lands in. A card or savings
          account needs its bank set here before a statement can be imported into it; a method with
          transactions on it cannot be deleted.
        </p>
      </div>

      <PaymentMethodsPanel getToken={accessToken} />
    </div>
  );
};

export default PaymentMethodsPage;
