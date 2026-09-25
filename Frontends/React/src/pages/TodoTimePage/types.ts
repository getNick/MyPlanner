export type ColorTheme = 'zinc' | 'indigo' | 'emerald' | 'amber' | 'rose' | 'cyan';

export interface ColorThemeOption {
  id: ColorTheme;
  name: string;
  swatchHex: string;
  accentBg: string;
  accentText: string;
  accentBorder: string;
  primaryBtnBg: string;
  primaryBtnText: string;
  primaryBtnHover: string;
  activeNavBg: string;
  activeNavText: string;
  badgeBg: string;
  badgeText: string;
  badgeBorder: string;
  timerHeaderBg: string;
  timerHeaderText: string;
}

export interface TaskItem {
  id: string;
  title: string;
  project: string;
  dueDate: string;
  priority: 'urgent' | 'high' | 'medium' | 'low';
  completed: boolean;
  tag: string;
}

export interface NoteItem {
  id: string;
  title: string;
  content: string;
  category: string;
  tags: string[];
  updatedAt: string;
  pinned: boolean;
}

export interface NavItem {
  id: string;
  label: string;
  iconName: string;
  path: string;
  badge?: string | number;
}

export interface TransactionItem {
  id: string;
  description: string;
  category: string;
  amount: number;
  type: 'income' | 'expense';
  date: string;
  account: string;
  status: 'cleared' | 'pending';
}

export interface OcrBillItem {
  id: string;
  vendor: string;
  invoiceNumber: string;
  date: string;
  amount: number;
  taxDeductible: boolean;
  confidenceScore: number;
  status: 'verified' | 'flagged' | 'processing';
  category: string;
}
