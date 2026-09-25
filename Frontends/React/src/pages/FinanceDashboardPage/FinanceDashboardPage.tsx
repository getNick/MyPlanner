import React, { useState, useEffect, useMemo } from "react";
import { Category, LineItem, SavedBill } from "../../types/receiptTypes";
import {
  TrendingUp,
  Coins,
  ChevronRight,
  ArrowRight,
  Search,
  Plus,
  Landmark,
  Receipt,
} from "lucide-react";
import { Link } from "react-router-dom";
import { useAuth } from "@clerk/clerk-react";
import FinanceService from "../../services/FinanceService";
import TransactionList from "../../components/TransactionList/TransactionList";
import { summarizeReport } from "../../domain/reportSumming";
import type { ReportRange } from "../../domain/reportSumming";
import type { BackendTransaction } from "../../types/receiptTypes";

// User setting — replace with real settings later
const USER_CURRENCY = "UAH";

// Predefined color palette for categories and subcategories
const CATEGORY_COLORS = [
  "#2563eb", // blue
  "#10b981", // emerald green
  "#f43f5e", // rose red
  "#f59e0b", // amber yellow
  "#8b5cf6", // violet purple
  "#ec4899", // pink fuchsia
  "#06b6d4", // cyan
  "#14b8a6", // teal
  "#ef4444", // red
  "#eab308", // yellow gold
  "#a855f7", // purple
  "#64748b", // slate gray
];

const SUBCATEGORY_COLORS = [
  "#2563eb", // blue
  "#10b981", // emerald green
  "#f43f5e", // rose red
  "#f59e0b", // amber yellow
  "#8b5cf6", // violet purple
  "#ec4899", // pink fuchsia
  "#06b6d4", // cyan
  "#14b8a6", // teal
  "#ef4444", // red
  "#eab308", // yellow gold
];

export default function FinanceDashboardPage() {
  const { getToken } = useAuth();
  const [savedBills, setSavedBills] = useState<SavedBill[]>([]);
  // The ledger rows as the backend returns them (Bank, Receipt, Manual, Reconciled) — kept next to
  // the SavedBill view model so the report summing runs over one row per transaction.
  const [transactions, setTransactions] = useState<BackendTransaction[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [refreshKey, setRefreshKey] = useState(0);
  const [isAddTransactionOpen, setIsAddTransactionOpen] = useState(false);
  const [manualDescription, setManualDescription] = useState("");
  const [manualAmount, setManualAmount] = useState("");
  const [manualCategory, setManualCategory] = useState("");
  const [manualSubcategory, setManualSubcategory] = useState("");
  const [receiptCategories, setReceiptCategories] = useState<Category[]>([]);
  const [manualTimestamp, setManualTimestamp] = useState(() => {
    const now = new Date();
    const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000);
    return local.toISOString().slice(0, 16);
  });
  const [createError, setCreateError] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const today = new Date();
  const localToday = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
  const [rangeFrom, setRangeFrom] = useState(`${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-01`);
  const [rangeTo, setRangeTo] = useState(localToday);
  const reportRange = useMemo<ReportRange>(
    () => ({ from: rangeFrom || null, to: rangeTo || null }),
    [rangeFrom, rangeTo],
  );

  // Single FinanceService instance — reused for load and delete operations.
  // Created lazily on first mount; getToken is stable from Clerk.
  const financeServiceRef = React.useRef<FinanceService | null>(null);
  if (!financeServiceRef.current) {
    financeServiceRef.current = new FinanceService(async () => {
      return await getToken({ template: "AspNetToken" });
    });
  }

  // Fetch only the selected report window; server query parameters prevent downloading all history.
  useEffect(() => {
    const loadTransactions = async () => {
      setIsLoading(true);
      try {
        const fs = financeServiceRef.current!;
        const transactions = await fs.getTransactions(reportRange);
        setTransactions(transactions);

        // Map backend entities to SavedBill shape — all transaction types shown
        const mapped: SavedBill[] = transactions.map((tx) => ({
          id: tx.id,
          merchantName: tx.description || "",
          timestamp: tx.timestamp,
          totalAmount: tx.amount, // authoritative total from backend
          currency: fs.mapCurrency(tx.currency),
          paymentMethod: undefined,
          items: tx.items.map((item) => ({
            name: item.name,
            fullName: item.fullName,
            quantity: item.quantity,
            unitPrice: item.pricePerUnit,
            totalPrice: item.totalPrice,
            category: item.category ?? undefined,
            subcategory: item.subcategory ?? undefined,
          })),
          additionalNotes: tx.additionalNotes ?? "",
          createdAt: tx.createdAt || new Date().toISOString(), // fallback to now
          tags: [],
          dataOrigin: tx.dataOrigin,
          moneyDelta: tx.moneyDelta,
        }));

        setSavedBills(mapped);
      } catch (err) {
        console.error("Failed to load transactions:", err);
      } finally {
        setIsLoading(false);
      }
    };

    loadTransactions();
  }, [getToken, reportRange, refreshKey]);

  useEffect(() => {
    let isCurrent = true;
    financeServiceRef.current!.getReceiptCategories().then((categories) => {
      if (isCurrent) setReceiptCategories(categories);
    });
    return () => { isCurrent = false; };
  }, []);

  const selectedManualCategory = receiptCategories.find((category) => category.name === manualCategory);
  const availableManualSubcategories = selectedManualCategory?.subcategories ?? [];

  const [searchTerm, setSearchTerm] = useState("");
  const [activeFilter, setActiveFilter] = useState<
    "all" | "alcohol" | "junk" | "highValue"
  >("all");
  const [drilldownCategory, setDrilldownCategory] = useState<string | null>(
    null,
  );
  const [hoveredSlice, setHoveredSlice] = useState<string | null>(null);
  const [expandedSubcategory, setExpandedSubcategory] = useState<string | null>(
    null,
  );



  // Subcategory assignment: use only the subcategory field from LineItem, fallback to "Other"
  const getSubcategory = (item: LineItem): string => {
    if (item.subcategory) return item.subcategory;
    return "Other";
  };

  // Helper functions for real category/subcategory checking
  const isAlcoholItem = (item: LineItem): boolean => {
    if (item.category?.toLowerCase() !== "groceries") return false;
    return getSubcategory(item) === "Alcohol";
  };

  const isJunkFoodItem = (item: LineItem): boolean => {
    if (item.category?.toLowerCase() !== "groceries") return false;
    const subcat = getSubcategory(item);
    return ["Snacks", "Sweets", "Beverages (non-alcoholic)"].includes(subcat);
  };

  // Report summing — pulled out of this component (domain/reportSumming.ts): the ledger rows and a
  // date range go in, the category matrix and Money Delta rollup come out. The dashboard sums over
  // these, not inline, so every row is counted once.
  const report = useMemo(
    () => summarizeReport(transactions, reportRange),
    [transactions, reportRange],
  );

  // 1. Calculate General Financial Indices
  const stats = useMemo(() => {
    let totalSpent = 0;
    let totalItems = 0;
    let totalTax = 0;
    let alcoholSpent = 0;
    let junkFoodSpent = 0;

    const merchantTotals: Record<string, number> = {};

    savedBills.forEach((bill) => {
      totalSpent += bill.totalAmount;

      // Group merchant names
      const merchant = bill.merchantName || "Unknown Merchant";
      merchantTotals[merchant] =
        (merchantTotals[merchant] || 0) + bill.totalAmount;

      bill.items.forEach((item) => {
        totalItems += item.quantity;

        // Calculate alcohol spending using helper function
        if (isAlcoholItem(item)) {
          alcoholSpent += item.totalPrice;
        }

        // Calculate junk food spending using helper function
        if (isJunkFoodItem(item)) {
          junkFoodSpent += item.totalPrice;
        }
      });
    });

    const averageBill =
      savedBills.length > 0 ? totalSpent / savedBills.length : 0;

    // Get dominant merchant
    let topMerchant = "N/A";
    let maxMerchantSpent = 0;
    Object.entries(merchantTotals).forEach(([m, val]) => {
      if (val > maxMerchantSpent) {
        maxMerchantSpent = val;
        topMerchant = m;
      }
    });

    return {
      totalSpent,
      totalItems,
      totalTax,
      alcoholSpent,
      junkFoodSpent,
      averageBill,
      topMerchant,
      categoryTotals: report.categoryMatrix,
      merchantTotals,
    };
  }, [savedBills, report]);

  // 1.5. Calculate subcategory stats for drilldown
  const subcategoryStats = useMemo(() => {
    // Every ledger row's chart amount comes from the tested report matrix. Only actual Bill Line
    // Items are drilldown detail; MCC/manual classification items categorize spend, they aren't
    // purchase detail for users to expand.
    const subTotals: Record<string, Record<string, number>> = report.categorySubcategoryMatrix;
    const subItems: Record<string, Record<string, LineItem[]>> = {};

    transactions.forEach((transaction) => {
      if (transaction.dataOrigin !== "Receipt" && transaction.dataOrigin !== "Reconciled") return;
      transaction.items.filter((item) => item.origin !== "AutoGenerated").forEach((item) => {
        const category = item.category || "Other";
        const subcategory = item.subcategory || "Other";
        subItems[category] ??= {};
        subItems[category][subcategory] ??= [];
        subItems[category][subcategory].push({
          id: item.id,
          name: item.name,
          fullName: item.fullName,
          quantity: item.quantity,
          unitPrice: item.pricePerUnit,
          totalPrice: item.totalPrice,
          category: item.category ?? undefined,
          subcategory: item.subcategory ?? undefined,
        });
      });
    });

    return { subTotals, subItems };
  }, [report, transactions]);

  const getCategoryColor = useMemo(() => {
    const colorMap: Record<string, string> = {};
    let nextIdx = 0;
    return (cat: string): string => {
      if (!colorMap[cat]) {
        colorMap[cat] = CATEGORY_COLORS[nextIdx % CATEGORY_COLORS.length];
        nextIdx++;
      }
      return colorMap[cat];
    };
  }, []);

  const getSubcategoryColor = useMemo(() => {
    const colorMap: Record<string, string> = {};
    let nextIdx = 0;
    return (sub: string): string => {
      if (!colorMap[sub]) {
        colorMap[sub] = SUBCATEGORY_COLORS[nextIdx % SUBCATEGORY_COLORS.length];
        nextIdx++;
      }
      return colorMap[sub];
    };
  }, []);

  // Format currency: symbol before amount ($10.00), code after (10.00 UAH)
  const formatCurrency = (
    value: number,
    curr?: string,
  ): React.ReactNode => {
    if (!curr) return <>{value.toFixed(2)}</>;
    // Single-char symbols ($, €, £) go before; 3-letter codes (UAH, PLN) go after
    const isSymbol = curr.length === 1;
    return (
      <>
        {isSymbol ? curr : null}
        {value.toFixed(2)}
        {!isSymbol ? ` ${curr}` : null}
      </>
    );
  };

  const currentSlices = useMemo(() => {
    if (drilldownCategory === null) {
      return Object.entries(stats.categoryTotals)
        .map(([cat, val]) => ({
          label: cat,
          value: val as number,
          color: getCategoryColor(cat),
          isClickable: true,
        }))
        .sort((a, b) => b.value - a.value);
    } else {
      const subEntries = subcategoryStats.subTotals[drilldownCategory] || {};
      return Object.entries(subEntries)
        .map(([sub, val]) => ({
          label: sub,
          value: val as number,
          color: getSubcategoryColor(sub),
          isClickable: false,
        }))
        .sort((a, b) => b.value - a.value);
    }
  }, [drilldownCategory, stats.categoryTotals, subcategoryStats]);

  const generatePiePaths = (
    slicesList: {
      label: string;
      value: number;
      color: string;
      isClickable?: boolean;
    }[],
  ) => {
    const total = slicesList.reduce((sum, s) => sum + s.value, 0);
    if (total === 0) return [];

    let accumulatedAngle = -Math.PI / 2; // top center

    return slicesList.map((slice) => {
      const percentage = (slice.value / Math.max(total, 0.01)) * 100;
      const angle = (slice.value / Math.max(total, 0.01)) * 2 * Math.PI;

      const startAngle = accumulatedAngle;
      const endAngle = accumulatedAngle + angle;
      accumulatedAngle = endAngle;

      const r = 80;
      const cx = 100;
      const cy = 100;

      const x1 = cx + r * Math.cos(startAngle);
      const y1 = cy + r * Math.sin(startAngle);
      const x2 = cx + r * Math.cos(endAngle);
      const y2 = cy + r * Math.sin(endAngle);

      const largeArcFlag = angle > Math.PI ? 1 : 0;

      let pathData = "";
      if (percentage >= 99.9) {
        pathData = `M ${cx} ${cy - r} A ${r} ${r} 0 1 1 ${cx - 0.01} ${cy - r} Z`;
      } else {
        pathData = `M ${cx} ${cy} L ${x1} ${y1} A ${r} ${r} 0 ${largeArcFlag} 1 ${x2} ${y2} Z`;
      }

      const labelAngle = startAngle + angle / 2;
      const labelR = 52;
      const labelX = cx + labelR * Math.cos(labelAngle);
      const labelY = cy + labelR * Math.sin(labelAngle);

      return {
        ...slice,
        percentage,
        pathData,
        labelX,
        labelY,
        angle,
        labelAngle,
      };
    });
  };

  const pieSlicesWithAngles = useMemo(() => {
    return generatePiePaths(currentSlices);
  }, [currentSlices]);

  const hoveredSliceDetails = useMemo(() => {
    const active = pieSlicesWithAngles.find((s) => s.label === hoveredSlice);
    if (active) {
      return {
        label: active.label,
        value: active.value,
        percent: active.percentage,
      };
    }
    const currentTotal = pieSlicesWithAngles.reduce(
      (sum, s) => sum + s.value,
      0,
    );
    return {
      label: drilldownCategory || "Total Spent",
      value: currentTotal,
      percent: 100,
    };
  }, [hoveredSlice, pieSlicesWithAngles, drilldownCategory]);

  // 2. Identify chronological trends for high-fidelity SVG Area Chart
  const trendData = useMemo(() => {
    // Sort bills chronologically by transaction date
    const sorted = [...savedBills].sort((a, b) =>
      (a.timestamp || a.createdAt).localeCompare(b.timestamp || b.createdAt)
    );

    // Group items by date to prevent duplicate days on x-axis
    const dateAggr: Record<string, number> = {};
    sorted.forEach((bill) => {
      // Use transaction date (timestamp) first, fall back to save date
      const dStr = bill.timestamp?.slice(0, 10) || bill.createdAt?.slice(0, 10) || "Unknown";
      dateAggr[dStr] = (dateAggr[dStr] || 0) + bill.totalAmount;
    });

    return Object.entries(dateAggr).map(([date, amount]) => ({
      date: date.replace("2026-", ""), // trim year for spacing elegance
      amount,
    }));
  }, [savedBills]);

  // 3. Filter bills - use real category/subcategory for alcohol/junk filters
  const filteredBills = useMemo(() => {
    return savedBills.filter((bill) => {
      // search match
      const searchMatch =
        (bill.merchantName?.toLowerCase() ?? "").includes(
          searchTerm.toLowerCase(),
        ) ||
        bill.items.some((it) =>
          it.name.toLowerCase().includes(searchTerm.toLowerCase()),
        );

      if (!searchMatch) return false;

      // Filter tags - use helper functions
      if (activeFilter === "alcohol") {
        return bill.items.some(isAlcoholItem);
      }
      if (activeFilter === "junk") {
        return bill.items.some(isJunkFoodItem);
      }
      if (activeFilter === "highValue") {
        return bill.totalAmount >= 50.0;
      }

      return true;
    });
  }, [savedBills, searchTerm, activeFilter]);

  const filteredTransactions = useMemo(() => {
    const visibleIds = new Set(filteredBills.map((bill) => bill.id));
    return transactions.filter((transaction) => visibleIds.has(transaction.id));
  }, [transactions, filteredBills]);

  // Indulgence warning algorithms
  const totalAmountSafe = Math.max(stats.totalSpent, 1);
  const alcoholPercentage = (stats.alcoholSpent / totalAmountSafe) * 100;
  const junkPercentage = (stats.junkFoodSpent / totalAmountSafe) * 100;
  const indulgencePercentage = alcoholPercentage + junkPercentage;

  // Render high-fidelity SVG chart coordinate calculations
  const maxAmount = useMemo(() => {
    const vals = trendData.map((t) => t.amount);
    return vals.length > 0 ? Math.max(...vals, 40) : 100;
  }, [trendData]);

  const chartPoints = useMemo(() => {
    if (trendData.length === 0) return "";
    const width = 450;
    const height = 120;
    const paddingX = 30;
    const paddingY = 15;

    const usableWidth = width - paddingX * 2;
    const usableHeight = height - paddingY * 2;

    const stepX =
      trendData.length > 1 ? usableWidth / (trendData.length - 1) : usableWidth;

    return trendData
      .map((d, i) => {
        const x = paddingX + i * stepX;
        const ratio = d.amount / maxAmount;
        const y = height - paddingY - ratio * usableHeight;
        return `${x},${y}`;
      })
      .join(" ");
  }, [trendData, maxAmount]);

  return (
    <div className="w-full flex flex-col gap-4 font-mono text-zinc-900" id="dashboard-root">
      <header className="flex flex-col sm:flex-row sm:items-end sm:justify-between gap-3 border-b border-slate-200 pb-4">
        <div>
          <p className="text-[10px] uppercase tracking-[0.13em] text-slate-500"><span className="text-zinc-900">◈</span> Module // Finance · Dashboard</p>
          <h1 className="mt-1 text-2xl font-bold tracking-tight uppercase text-zinc-900">Finance Dashboard</h1>
        </div>
        <div aria-label="Global report date range" className="flex flex-wrap items-end gap-2">
          <label className="text-[9px] uppercase tracking-wider text-slate-500">From
            <input aria-label="Report start date" className="block mt-1 border border-slate-300 rounded-sm bg-white px-2 py-1.5 text-[10px] text-zinc-900 focus:border-zinc-900 outline-none" type="date" value={rangeFrom} onChange={(event) => setRangeFrom(event.target.value)} />
          </label>
          <label className="text-[9px] uppercase tracking-wider text-slate-500">To
            <input aria-label="Report end date" className="block mt-1 border border-slate-300 rounded-sm bg-white px-2 py-1.5 text-[10px] text-zinc-900 focus:border-zinc-900 outline-none" type="date" value={rangeTo} onChange={(event) => setRangeTo(event.target.value)} />
          </label>
        </div>
      </header>

      {/* Overview statistical cards (Bento Grid) */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        {/* Total Spend */}
        <div className="bg-white border border-slate-300 p-5 rounded-sm">
          <p className="text-[10px] font-bold text-slate-500 uppercase tracking-widest">
            Aggregate Expenditure
          </p>
          <div className="flex items-baseline gap-1.5 mt-2">
            <span className="text-2xl font-bold text-blue-600 font-sans tabular-nums">
              {formatCurrency(stats.totalSpent, USER_CURRENCY)}
            </span>
          </div>
          <div className="flex items-center gap-1.5 mt-3 text-[10px] text-slate-500">
            <Coins className="w-3.5 h-3.5 text-blue-500" />
            <span>Across {savedBills.length} transactions</span>
          </div>
        </div>

        {/* Indulgence Metrics: Alcohol index - from Groceries/Alcohol */}
        <div className="bg-white border border-slate-300 p-5 rounded-sm flex flex-col justify-between">
          <div>
            <p className="text-[10px] font-bold text-slate-500 uppercase tracking-widest block">
              Alcohol Index
            </p>
            <div className="flex items-baseline gap-1 mt-2">
              <span className="text-2xl font-bold text-zinc-900 font-sans">
                {formatCurrency(stats.alcoholSpent, USER_CURRENCY)}
              </span>
              <span className="text-xs font-semibold text-slate-500">
                ({alcoholPercentage.toFixed(0)}%)
              </span>
            </div>
          </div>
          <p className="text-[10px] text-slate-500 mt-3 truncate">
            {alcoholPercentage.toFixed(0)}% of total spend
          </p>
        </div>

        {/* Indulgence Metrics: Junk food index - from Groceries subcategories */}
        <div className="bg-white border border-slate-300 p-5 rounded-sm flex flex-col justify-between">
          <div>
            <p className="text-[10px] font-bold text-slate-500 uppercase tracking-widest block">
              Junk Food Allocation
            </p>
            <div className="flex items-baseline gap-1 mt-2">
              <span className="text-2xl font-bold text-zinc-900 font-sans">
                {formatCurrency(stats.junkFoodSpent, USER_CURRENCY)}
              </span>
              <span className="text-xs font-semibold text-slate-500">
                ({junkPercentage.toFixed(0)}%)
              </span>
            </div>
          </div>
          <p className="text-[10px] text-slate-500 mt-3 truncate">
            {junkPercentage.toFixed(0)}% of total spend
          </p>
        </div>

        {/* Average and Top vendor */}
        <div className="bg-white border border-slate-300 p-5 rounded-sm flex flex-col justify-between">
          <div>
            <p className="text-[10px] font-bold text-slate-500 uppercase tracking-widest">
              Core Vendor
            </p>
            <p className="text-sm font-bold text-zinc-900 mt-2 truncate">
              {stats.topMerchant}
            </p>
          </div>
          <div className="text-[10px] text-slate-500 mt-2 flex justify-between pt-2 border-t border-slate-100">
            <span>Average Receipt:</span>
            <span className="font-mono text-slate-700 font-bold">
              {formatCurrency(stats.averageBill, USER_CURRENCY)}
            </span>
          </div>
        </div>
      </div>

      {/* Main visual charting grid */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* The chart and breakdown sit side by side on wide screens and stack on mobile. */}
        <div className="contents">
          {/* Spend trend line graph */}
          <div className="bg-white border border-slate-300 p-5 rounded-sm">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h4 className="text-xs font-bold uppercase tracking-wider text-zinc-900">
                  Spending Trend
                </h4>
                <p className="text-[10px] text-slate-500 mt-1">
                  Daily expenses in the selected date range
                </p>
              </div>
              <TrendingUp className="w-4 h-4 text-slate-500" />
            </div>

            <div className="mt-4 flex flex-col items-center">
              {trendData.length > 0 ? (
                <div className="w-full">
                  {/* Custom beautifully designed responsive SVG Area Chart */}
                  <svg
                    viewBox="0 0 450 120"
                    className="w-full h-[150px] overflow-visible select-none"
                  >
                    <defs>
                      <linearGradient id="areaGrad" x1="0" y1="0" x2="0" y2="1">
                        <stop
                          offset="0%"
                          stopColor="#2563eb"
                          stopOpacity="0.2"
                        />
                        <stop
                          offset="100%"
                          stopColor="#2563eb"
                          stopOpacity="0.0"
                        />
                      </linearGradient>
                    </defs>

                    {/* horizontal helper gridlines */}
                    <line
                      x1="30"
                      y1="15"
                      x2="420"
                      y2="15"
                      stroke="#f1f5f9"
                      strokeWidth="1"
                      strokeDasharray="3"
                    />
                    <line
                      x1="30"
                      y1="60"
                      x2="420"
                      y2="60"
                      stroke="#f1f5f9"
                      strokeWidth="1"
                      strokeDasharray="3"
                    />
                    <line
                      x1="30"
                      y1="105"
                      x2="420"
                      y2="105"
                      stroke="#f1f5f9"
                      strokeWidth="1"
                    />

                    {/* Left edge Axis scale */}
                    <text
                      x="8"
                      y="18"
                      fill="#94a3b8"
                      className="text-[7px] font-mono"
                    >
                      {formatCurrency(maxAmount, USER_CURRENCY)}
                    </text>
                    <text
                      x="8"
                      y="108"
                      fill="#94a3b8"
                      className="text-[7px] font-mono"
                    >
                      0
                    </text>

                    {/* Compiled Area representation */}
                    <path
                      d={`M ${trendData.length > 1 ? "30" : "225"},105 L ${chartPoints} L ${trendData.length > 1 ? "420" : "225"},105 Z`}
                      fill="url(#areaGrad)"
                    />

                    {/* Line path representation */}
                    <polyline
                      fill="none"
                      stroke="#2563eb"
                      strokeWidth="2.5"
                      points={chartPoints}
                    />

                    {/* Nodes and tooltip helpers */}
                    {trendData.map((d, i) => {
                      const width = 450;
                      const height = 120;
                      const paddingX = 30;
                      const paddingY = 15;
                      const stepX =
                        trendData.length > 1
                          ? (width - paddingX * 2) / (trendData.length - 1)
                          : width - paddingX * 2;
                      const x = paddingX + i * stepX;
                      const y =
                        height -
                        paddingY -
                        (d.amount / maxAmount) * (height - paddingY * 2);

                      return (
                        <g key={i} className="group cursor-pointer">
                          <circle
                            cx={x}
                            cy={y}
                            r="4"
                            fill="#ffffff"
                            stroke="#2563eb"
                            strokeWidth="2"
                            className="transition-all hover:r-5 hover:stroke-blue-700"
                          />
                          <text
                            x={x}
                            y={y - 8}
                            textAnchor="middle"
                            fill="#1e293b"
                            className="text-[8px] font-mono font-bold bg-white opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none"
                          >
                            {formatCurrency(d.amount, USER_CURRENCY)}
                          </text>
                        </g>
                      );
                    })}
                  </svg>

                  {/* X axis labels mapping dates */}
                  <div className="flex justify-between px-7 text-[9px] font-mono text-slate-500 mt-2">
                    {trendData.map((d, i) => (
                      <span
                        key={i}
                        className="truncate max-w-[40px] text-center"
                      >
                        {d.date}
                      </span>
                    ))}
                  </div>
                </div>
              ) : (
                <div className="py-12 text-slate-500 italic text-xs">
                  No trend coordinates available.
                </div>
              )}
            </div>
          </div>

          {/* Interactive Category breakdown & subcategory drilldown meter rows */}
          <div className="bg-white border border-slate-300 p-5 rounded-sm flex-1 flex flex-col">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-100 pb-4 mb-4">
              <div>
                <h4 className="text-xs font-bold uppercase tracking-wider text-zinc-900">
                  Interactive Budget Breakdown &amp; Drilldown
                </h4>
                <p className="text-[11px] text-slate-500 mt-0.5">
                  {drilldownCategory
                    ? `Showing detailed spending subcategories for "${drilldownCategory}"`
                    : "Interact with the donut slices or category rows to explore subcategories"}
                </p>
              </div>

              {/* Navigation path breadcrumb */}
              <div className="flex items-center gap-1.5 shrink-0">
                <button
                  onClick={() => {
                    setDrilldownCategory(null);
                    setHoveredSlice(null);
                    setExpandedSubcategory(null);
                  }}
                  className={`text-[10px] font-bold uppercase px-2.5 py-1 rounded transition flex items-center gap-1 leading-none ${
                    drilldownCategory
                      ? "bg-slate-100/80 text-slate-600 hover:bg-slate-200 cursor-pointer"
                      : "bg-blue-50 text-blue-600 cursor-default font-bold"
                  }`}
                  disabled={!drilldownCategory}
                >
                  Categories
                </button>
                {drilldownCategory && (
                  <>
                    <ChevronRight className="w-3 h-3 text-slate-500" />
                    <span className="text-[10px] bg-blue-50 text-blue-700 font-extrabold uppercase px-2 py-1 rounded leading-none">
                      {drilldownCategory}
                    </span>
                  </>
                )}
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-12 gap-6 items-center">
              {/* SVG Donut Chart Column */}
              <div className="md:col-span-5 flex flex-col items-center justify-center relative p-4">
                {pieSlicesWithAngles.length > 0 ? (
                  <div className="w-full max-w-[360px] relative">
                    <svg
                      viewBox="0 0 200 200"
                      className="w-full h-full overflow-visible select-none"
                    >
                      <g className="transition-all duration-300">
                        {pieSlicesWithAngles.map((slice, idx) => {
                          const isHovered = hoveredSlice === slice.label;
                          // Calculate slice offset vector for elegant transform
                          const offsetDist = isHovered ? 6 : 0;
                          const tx = (
                            offsetDist * Math.cos(slice.labelAngle)
                          ).toFixed(1);
                          const ty = (
                            offsetDist * Math.sin(slice.labelAngle)
                          ).toFixed(1);

                          return (
                            <path
                              key={idx}
                              d={slice.pathData}
                              fill={slice.color}
                              className="transition-all duration-250 cursor-pointer stroke-white outline-none"
                              strokeWidth={isHovered ? 1.5 : 1}
                              style={{
                                transform: `translate(${tx}px, ${ty}px)`,
                                transition:
                                  "all 0.25s cubic-bezier(0.16, 1, 0.3, 1)",
                                filter: isHovered
                                  ? "drop-shadow(0 4px 6px rgba(0,0,0,0.1)) brightness(1.05)"
                                  : "none",
                              }}
                              onMouseEnter={() => setHoveredSlice(slice.label)}
                              onMouseLeave={() => setHoveredSlice(null)}
                              onClick={() => {
                                if (slice.isClickable) {
                                  setDrilldownCategory(slice.label);
                                  setHoveredSlice(null);
                                  setExpandedSubcategory(null);
                                }
                              }}
                            />
                          );
                        })}
                      </g>

                      {/* Donut cutout overlay */}
                      <circle
                        cx="100"
                        cy="100"
                        r="48"
                        fill="#ffffff"
                        className="pointer-events-none"
                      />

                      {/* Floating Tooltip in middle of donut cutout */}
                      <g className="pointer-events-none">
                        <text
                          x="100"
                          y="88"
                          textAnchor="middle"
                          fill="#94a3b8"
                          className="text-[7.5px] font-bold uppercase tracking-widest leading-none"
                        >
                          {hoveredSliceDetails.label.length > 15
                            ? hoveredSliceDetails.label.substring(0, 13) + ".."
                            : hoveredSliceDetails.label}
                        </text>
                        <text
                          x="100"
                          y="106"
                          textAnchor="middle"
                          fill="#1e293b"
                          className="text-[12.5px] font-bold font-mono leading-none"
                        >
                          {formatCurrency(hoveredSliceDetails.value, USER_CURRENCY)}
                        </text>
                        <text
                          x="100"
                          y="119"
                          textAnchor="middle"
                          fill="#64748b"
                          className="text-[7.5px] font-mono leading-none font-bold"
                        >
                          {hoveredSliceDetails.percent.toFixed(1)}%
                        </text>
                      </g>
                    </svg>
                  </div>
                ) : (
                  <div className="py-12 text-slate-500 italic text-xs text-center">
                    No budget coordinate records found.
                  </div>
                )}

                {drilldownCategory && (
                  <button
                    onClick={() => {
                      setDrilldownCategory(null);
                      setHoveredSlice(null);
                      setExpandedSubcategory(null);
                    }}
                    className="mt-3 text-[10px] text-slate-500 font-extrabold hover:text-blue-600 transition flex items-center gap-1 py-1 px-2.5 bg-slate-50 border border-slate-200/80 rounded cursor-pointer"
                  >
                    <span>← Back to Categories</span>
                  </button>
                )}
              </div>

              {/* Progress Rows Column */}
              <div className="md:col-span-7 space-y-3 max-h-[500px] overflow-y-auto pr-1">
                {pieSlicesWithAngles.length > 0 ? (
                  pieSlicesWithAngles.map((slice, idx) => {
                    const ratio = slice.percentage;
                    const isHovered = hoveredSlice === slice.label;

                    // Count items inside this subcategory or category to enrich labels
                    let itemsCount = 0;
                    let lineItemsForView: LineItem[] = [];

                    if (drilldownCategory) {
                      lineItemsForView =
                        subcategoryStats.subItems[drilldownCategory]?.[
                          slice.label
                        ] || [];
                      itemsCount = lineItemsForView.reduce(
                        (total, cur) => total + cur.quantity,
                        0,
                      );
                    } else {
                      // Top level total items - use real category
                      savedBills.forEach((bill) => {
                        bill.items.forEach((item) => {
                          let parentCat = item.category || "Other";
                          if (parentCat === slice.label) {
                            itemsCount += item.quantity;
                          }
                        });
                      });
                    }

                    const hasLineItemDetails = lineItemsForView.length > 0;
                    const isExpanded = expandedSubcategory === slice.label;

                    return (
                      <div
                        key={idx}
                        className={`p-2.5 rounded-sm border transition-all ${
                          isHovered
                            ? "bg-slate-50/80 border-slate-300 shadow-sm"
                            : "bg-white border-slate-100"
                        }`}
                        onMouseEnter={() => setHoveredSlice(slice.label)}
                        onMouseLeave={() => setHoveredSlice(null)}
                      >
                        <div className="flex justify-between items-center text-xs">
                          <button
                            onClick={() => {
                              if (slice.isClickable) {
                                setDrilldownCategory(slice.label);
                                setHoveredSlice(null);
                                setExpandedSubcategory(null);
                              } else if (drilldownCategory && hasLineItemDetails) {
                                // Only actual Bill Line Items can be expanded; classification items
                                // contribute chart amounts but are not purchase detail.
                                setExpandedSubcategory(
                                  isExpanded ? null : slice.label,
                                );
                              }
                            }}
                            disabled={!slice.isClickable && !hasLineItemDetails}
                            className={`px-2 py-0.5 border rounded font-semibold text-[10px] uppercase tracking-wide flex items-center justify-between gap-1.5 transition-all text-left max-w-full ${
                              slice.isClickable
                                ? "bg-slate-50 hover:bg-slate-100 text-zinc-900 border-slate-200 cursor-pointer"
                                : hasLineItemDetails
                                  ? "bg-blue-50/50 text-blue-700 border-blue-100/50 cursor-pointer"
                                  : "bg-blue-50/50 text-blue-700 border-blue-100/50 cursor-default"
                            }`}
                          >
                            <span>{slice.label}</span>
                            {slice.isClickable && (
                              <ArrowRight className="w-2.5 h-2.5 text-slate-500 shrink-0" />
                            )}
                            {!slice.isClickable && drilldownCategory && (
                              <span className="text-[8px] bg-blue-100 text-blue-800 px-1.5 rounded-sm size-min font-mono tracking-normal capitalize shrink-0 leading-none py-0.5">
                                {hasLineItemDetails ? `${lineItemsForView.length} items` : "Classified spend"}
                              </span>
                            )}
                          </button>

                          <div className="font-mono text-slate-500 flex items-center gap-1.5 shrink-0">
                            <span className="font-bold text-zinc-900 font-mono">
                              {formatCurrency(slice.value, USER_CURRENCY)}
                            </span>
                            <span className="text-[10px] text-slate-500 font-medium font-mono">
                              ({ratio.toFixed(0)}%)
                            </span>
                          </div>
                        </div>

                        {/* Visual bar meter */}
                        <div
                          className="w-full h-2 bg-slate-100 rounded-full overflow-hidden mt-2 cursor-pointer"
                          onClick={() => {
                            if (slice.isClickable) {
                              setDrilldownCategory(slice.label);
                              setHoveredSlice(null);
                              setExpandedSubcategory(null);
                            } else if (drilldownCategory && hasLineItemDetails) {
                              setExpandedSubcategory(
                                isExpanded ? null : slice.label,
                              );
                            }
                          }}
                        >
                          <div
                            className="h-full rounded-full transition-all"
                            style={{
                              width: `${ratio}%`,
                              backgroundColor: slice.color,
                              transitionDuration: "0.4s",
                            }}
                          ></div>
                        </div>

                        {/* Expansive product line listing for subcategories */}
                        {!slice.isClickable &&
                          drilldownCategory &&
                          isExpanded &&
                          lineItemsForView.length > 0 && (
                            <div className="mt-2 text-[11px] bg-slate-50 rounded-sm border border-slate-200/60 divide-y divide-slate-100 overflow-hidden">
                              {lineItemsForView.map((prod, pIdx) => (
                                <div
                                  key={pIdx}
                                  className="px-2.5 py-1.5 flex justify-between gap-3 text-slate-600 hover:bg-slate-100/60 transition"
                                >
                                  <div className="truncate flex items-start gap-1">
                                    <span className="font-mono text-[9px] bg-slate-200 text-slate-700 rounded px-1 shrink-0 mt-0.5 font-bold">
                                      {prod.quantity}x
                                    </span>
                                    <span className="truncate font-medium text-slate-700">
                                      {prod.name}
                                    </span>
                                  </div>
                                  <div className="font-mono text-right text-slate-500 select-none shrink-0">
                                    {formatCurrency(prod.totalPrice, USER_CURRENCY)}
                                  </div>
                                </div>
                              ))}
                            </div>
                          )}
                      </div>
                    );
                  })
                ) : (
                  <div className="py-12 text-slate-500 italic text-xs text-center">
                    No expenditures identified for search filters.
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

      </div>

      <section aria-label="Finance Transactions" className="w-full bg-white border border-slate-300 p-4 sm:p-5">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-200 pb-4 mb-4">
          <div className="flex items-center gap-3">
            <h3 className="text-sm font-bold uppercase tracking-wide text-zinc-900">Finance Transactions</h3>
            <button type="button" onClick={() => { setCreateError(null); setIsAddTransactionOpen(true); }} className="inline-flex items-center gap-1.5 border border-slate-300 bg-white px-2.5 py-1.5 text-[10px] font-bold uppercase tracking-wide hover:bg-slate-50">
              <Plus className="h-3 w-3" /> Add Transaction
            </button>
          </div>
          <div className="relative w-full sm:max-w-sm">
            <Search className="absolute left-2.5 top-2.5 w-3.5 h-3.5 text-slate-500" />
            <input aria-label="Search transactions" type="text" placeholder="Search vendor or product..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} className="w-full text-xs pl-8 pr-3 py-2 border border-slate-300 rounded-sm outline-none focus:border-slate-500" />
          </div>
        </div>
        <div className="flex flex-wrap gap-2 mb-3">
          <button onClick={() => setActiveFilter("all")} className={`py-1 px-2.5 border border-slate-300 rounded-sm text-[10px] font-mono ${activeFilter === "all" ? "bg-slate-800 text-white" : "bg-slate-50 text-slate-700"}`}>All</button>
          <button onClick={() => setActiveFilter("alcohol")} className={`py-1 px-2.5 border border-slate-300 rounded-sm text-[10px] font-mono ${activeFilter === "alcohol" ? "bg-slate-800 text-white" : "bg-slate-50 text-slate-700"}`}>Alcohol</button>
          <button onClick={() => setActiveFilter("junk")} className={`py-1 px-2.5 border border-slate-300 rounded-sm text-[10px] font-mono ${activeFilter === "junk" ? "bg-slate-800 text-white" : "bg-slate-50 text-slate-700"}`}>Junk Food</button>
          <button onClick={() => setActiveFilter("highValue")} className={`py-1 px-2.5 border border-slate-300 rounded-sm text-[10px] font-mono ${activeFilter === "highValue" ? "bg-slate-800 text-white" : "bg-slate-50 text-slate-700"}`}>&gt;$50</button>
        </div>
        <TransactionList
          transactions={filteredTransactions}
          label="Finance transactions"
          emptyMessage="No matching transactions found. Clear search parameters to view list."
          onDelete={async (transaction) => {
            if (!window.confirm("Are you sure you want to delete this transaction?")) return;
            try {
              await financeServiceRef.current!.deleteTransaction(transaction.id);
              setSavedBills((prev) => prev.filter((bill) => bill.id !== transaction.id));
              setTransactions((prev) => prev.filter((row) => row.id !== transaction.id));
            } catch (err) {
              console.error("Failed to delete transaction:", err);
            }
          }}
        />
      </section>

      {isAddTransactionOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <section role="dialog" aria-modal="true" aria-labelledby="add-transaction-title" className="w-full max-w-md border border-slate-300 bg-white p-5 shadow-xl">
            <h2 id="add-transaction-title" className="text-sm font-bold uppercase tracking-wide">Add Transaction</h2>
            <form className="mt-4 space-y-3" onSubmit={async (event) => {
              event.preventDefault();
              setIsCreating(true);
              setCreateError(null);
              let createdTransactionId: string | null = null;
              try {
                createdTransactionId = await financeServiceRef.current!.createTransaction({
                  type: "Expense",
                  paymentMethodId: null,
                  timestamp: new Date(manualTimestamp).toISOString(),
                  amount: Number(manualAmount),
                  currency: "UAH",
                  description: manualDescription.trim(),
                  additionalNotes: null,
                  dataOrigin: "Manual",
                });
                if (!createdTransactionId) throw new Error("The transaction was created without an ID.");
                await financeServiceRef.current!.createTransactionItem(createdTransactionId, {
                  name: manualDescription.trim(),
                  fullName: manualDescription.trim(),
                  category: manualCategory,
                  subcategory: manualSubcategory,
                  quantity: 1,
                  pricePerUnit: Number(manualAmount),
                  totalPrice: Number(manualAmount),
                  origin: "ManualInput",
                });
                setIsAddTransactionOpen(false);
                setManualDescription("");
                setManualAmount("");
                setManualCategory("");
                setManualSubcategory("");
                setRefreshKey((key) => key + 1);
              } catch (error) {
                console.error("Failed to create manual transaction:", error);
                if (createdTransactionId) {
                  try {
                    await financeServiceRef.current!.deleteTransaction(createdTransactionId);
                  } catch (rollbackError) {
                    console.error("Failed to clean up uncategorized manual transaction:", rollbackError);
                    setCreateError("The transaction was saved, but its category could not be saved. Refresh the ledger and delete the uncategorized transaction.");
                    setRefreshKey((key) => key + 1);
                    return;
                  }
                }
                setCreateError("Could not save transaction and its category. Check your connection and try again; your entries are still here.");
              } finally {
                setIsCreating(false);
              }
            }}>
              <section aria-labelledby="upload-section-title" className="border border-slate-200 bg-slate-50 p-3">
                <h3 id="upload-section-title" className="mb-2 text-xs font-bold uppercase tracking-wide text-slate-600">Upload</h3>
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                  <Link to="/finance/bank" className="flex min-h-12 items-center gap-3 border border-slate-300 bg-white px-3 py-2 text-xs font-semibold text-zinc-800 hover:bg-slate-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-600">
                    <Landmark aria-hidden="true" className="h-5 w-5 shrink-0 text-slate-600" />
                    <span>Bank statement</span>
                  </Link>
                  <Link to="/finance/shopping-bills" className="flex min-h-12 items-center gap-3 border border-slate-300 bg-white px-3 py-2 text-xs font-semibold text-zinc-800 hover:bg-slate-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-600">
                    <Receipt aria-hidden="true" className="h-5 w-5 shrink-0 text-slate-600" />
                    <span>Shopping Bill</span>
                  </Link>
                </div>
              </section>
              <div className="flex items-center gap-3 py-1 text-[10px] font-bold uppercase tracking-wider text-slate-500">
                <span className="h-px flex-1 bg-slate-300" />
                <span>Or enter manually</span>
                <span className="h-px flex-1 bg-slate-300" />
              </div>
              <div className="border border-slate-200 bg-slate-50 p-3">
                <div className="grid gap-3">
                  <label className="block text-xs">Description
                    <input aria-label="Description" required value={manualDescription} onChange={(event) => setManualDescription(event.target.value)} className="mt-1 block w-full border border-slate-300 px-2 py-1.5" />
                  </label>
                  <label className="block text-xs">Amount (UAH)
                    <input aria-label="Amount" required min="0.01" step="0.01" type="number" value={manualAmount} onChange={(event) => setManualAmount(event.target.value)} className="mt-1 block w-full border border-slate-300 px-2 py-1.5" />
                  </label>
                  <div role="group" aria-label="Category and Subcategory" className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <label className="block text-xs">Category
                      <select aria-label="Category" required value={manualCategory} onChange={(event) => { setManualCategory(event.target.value); setManualSubcategory(""); }} className="mt-1 block w-full border border-slate-300 px-2 py-1.5">
                        <option value="">Select a category</option>
                        {receiptCategories.map((category) => <option key={category.name} value={category.name}>{category.name}</option>)}
                      </select>
                    </label>
                    <label className="block text-xs">Subcategory
                      <select aria-label="Subcategory" required disabled={!selectedManualCategory} value={manualSubcategory} onChange={(event) => setManualSubcategory(event.target.value)} className="mt-1 block w-full border border-slate-300 px-2 py-1.5 disabled:bg-slate-100">
                        <option value="">Select a subcategory</option>
                        {availableManualSubcategories.map((subcategory) => <option key={subcategory} value={subcategory}>{subcategory}</option>)}
                      </select>
                    </label>
                  </div>
                  <label className="block text-xs">Timestamp
                    <input aria-label="Timestamp" required type="datetime-local" value={manualTimestamp} onChange={(event) => setManualTimestamp(event.target.value)} className="mt-1 block w-full border border-slate-300 px-2 py-1.5" />
                  </label>
                </div>
              </div>
              {createError && <p role="alert" className="text-sm text-rose-700">{createError}</p>}
              <div className="flex justify-end gap-2 pt-2">
                <button type="button" onClick={() => setIsAddTransactionOpen(false)} className="border border-slate-300 px-3 py-1.5 text-xs">Cancel</button>
                <button type="submit" disabled={isCreating} className="bg-zinc-900 px-3 py-1.5 text-xs font-bold text-white disabled:opacity-50">{isCreating ? "Saving…" : "Save transaction"}</button>
              </div>
            </form>
          </section>
        </div>
      )}
    </div>
  );
}
