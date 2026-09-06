# React Design Style Guide

The visual language shared by the showcase views (`OcrBillsView`, `TodoTimeView`)
and the components rendered inside them (e.g. `BillReportPanel`). Extracted from
the live showcase code — adopt it wholesale, do not invent a parallel look.

## Typography

- **Font:** `font-mono` everywhere (the whole app is monospace).
- **Big page titles:** `text-2xl font-bold text-zinc-900 tracking-tight`.
- **Section / module headers:** `text-xs font-bold uppercase tracking-wider text-zinc-900` (+ optional `w-4 h-4` icon).
- **Module labels (above a title):** `text-xs text-slate-500 uppercase tracking-widest`.
- **Inline field labels:** `text-[11px] text-slate-600 uppercase` (forms) — or `text-xs font-semibold text-slate-600 block` for stacked labels.
- **Table column headers:** `text-[9px] font-bold uppercase tracking-wider text-slate-400`.
- **Captions / meta:** `text-[10px] text-slate-500`.
- **Body:** `text-xs` / `text-sm`.

## Colors

- **Page background:** `bg-slate-50`.
- **Card background:** `bg-white`.
- **Card border:** `border border-slate-300`.
- **Internal dividers:** `border-b border-slate-200`.
- **Row separators:** `divide-y divide-slate-100`.
- **Primary text:** `text-zinc-900` (headings / bold labels), `text-slate-800` (body), `text-slate-600` / `text-slate-500` (labels / captions), `text-slate-400` (muted).
- **Money / positive:** `text-emerald-700` (bold). Right-aligned, `tabular-nums`.
- **Dark accent block:** `bg-zinc-950` / `bg-zinc-900` with `border-zinc-800`, white text.
- **Destructive:** `text-rose-600`, `bg-rose-50`, `hover:bg-rose-100`.
- **Input background:** `bg-slate-50`.
- **Focus:** `focus:border-zinc-900` (a dark border swap). **Not** a blue ring.

## Shape

- **Corners:** `rounded-sm` (near-square, ~2px) or `rounded-none`. **Never** `rounded-md` / `rounded-lg` / `rounded-full` for structure.
- Capsules / badges: `rounded-sm` (not `rounded-full`).
- Buttons: `rounded-sm`.

## Layout & mechanics

- **Section header row:** `flex justify-between` with `border-b border-slate-200 pb-3/4`; bold label left, `text-[10px]` caption right.
- **Numeric columns:** right-aligned + `tabular-nums`.
- **All labels / headers:** uppercase with tracking.
- **Empty state:** centered bold title + caption, dashed border box.
- **Add-row control:** `border-dashed border-slate-300`, centered icon + label.

## Known deviations to fix when porting a component here

- Do **not** use `rounded-lg` / `rounded-md` → `rounded-sm`.
- Do **not** use blue focus rings (`focus:ring-blue-500`) → `focus:border-zinc-900`.
- Do **not** use `font-sans` inline overrides → keep `font-mono`.
- Money should be `emerald-700`, not `emerald-600`.
- Bold labels should be `zinc-900`, not `slate-800`.
