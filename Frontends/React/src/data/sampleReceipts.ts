import { ReceiptData } from "../types/receiptTypes";

export interface DemoReceipt {
  id: string;
  name: string;
  logo: string;
  totalSum: number;
  currency: string;
  merchant: string;
  date: string;
  time: string;
  rawTextHtml?: string;
  data: ReceiptData;
}

export const DEMO_RECEIPTS: DemoReceipt[] = [
  {
    id: "target-groceries",
    name: "Targét Hypermarket",
    logo: "🎯",
    totalSum: 64.38,
    currency: "$",
    merchant: "Target Stores",
    date: "2026-05-12",
    time: "14:23",
    data: {
      merchantName: "Target Stores Inc.",
      timestamp: "2026-05-12T14:23:00Z",
      totalAmount: 97.80,
      currency: "$",
      paymentMethod: "Visa Debit (*4299)",
      additionalNotes: "Store #1924 • 1201 Broadway, New York, NY • Cashier: Sarah M.",
      items: [
        {
          name: "Organic Strawberries 1lb",
          fullName: "Organic Strawberries 1lb",
          quantity: 2,
          unitPrice: 4.99,
          totalPrice: 9.98,
          category: "Groceries",
          subcategory: "Fruits & Veggies"
        },
        {
          name: "Fresh Grass-Fed Angus Ribeye",
          fullName: "Fresh Grass-Fed Angus Ribeye",
          quantity: 1,
          unitPrice: 18.50,
          totalPrice: 18.50,
          category: "Groceries",
          subcategory: "Meat"
        },
        {
          name: "Organic Whole Milk 1 Gal",
          fullName: "Organic Whole Milk 1 Gal",
          quantity: 1,
          unitPrice: 4.50,
          totalPrice: 4.50,
          category: "Groceries",
          subcategory: "Dairy & Eggs"
        },
        {
          name: "Sharp Cheddar Cheese Block",
          fullName: "Sharp Cheddar Cheese Block",
          quantity: 1,
          unitPrice: 5.95,
          totalPrice: 5.95,
          category: "Groceries",
          subcategory: "Dairy & Cheese"
        },
        {
          name: "Fresh Broccoli Crowns",
          fullName: "Fresh Broccoli Crowns",
          quantity: 1,
          unitPrice: 2.99,
          totalPrice: 2.99,
          category: "Groceries",
          subcategory: "Fruits & Veggies"
        },
        {
          name: "Premium Bluetooth Earbuds (Black)",
          fullName: "Premium Bluetooth Earbuds (Black)",
          quantity: 1,
          unitPrice: 29.99,
          totalPrice: 29.99,
          category: "Electronics",
          subcategory: "Gadgets & Accs"
        },
        {
          name: "Cotton Sports Socks 3-Pack",
          fullName: "Cotton Sports Socks 3-Pack",
          quantity: 1,
          unitPrice: 8.50,
          totalPrice: 8.50,
          category: "Apparel",
          subcategory: "Clothing"
        },
        {
          name: "Whole Grain Blend Sliced Bread",
          fullName: "Whole Grain Blend Sliced Bread",
          quantity: 3,
          unitPrice: 3.67,
          totalPrice: 11.01,
          category: "Groceries",
          subcategory: "Bakery"
        }
      ]
    }
  },
  {
    id: "cafe-bite",
    name: "L'Aroma Espresso Bar",
    logo: "☕",
    totalSum: 22.50,
    currency: "€",
    merchant: "L'Aroma Espresso Bar",
    date: "2026-06-01",
    time: "09:45",
    data: {
      merchantName: "L'Aroma Espresso Bar",
      timestamp: "2026-06-01T09:45:00Z",
      totalAmount: 22.50,
      currency: "€",
      paymentMethod: "Apple Pay (Mastercard)",
      additionalNotes: "Table 4 • Order #A8910 • VAT #DE812345678",
      items: [
        {
          name: "Freshly Brewed Cappuccino (Large)",
          fullName: "Freshly Brewed Cappuccino (Large)",
          quantity: 2,
          unitPrice: 4.50,
          totalPrice: 9.00,
          category: "Dining & Takeaway",
          subcategory: "Cafes & Coffee"
        },
        {
          name: "Avocado Sourdough Toast with Egg",
          fullName: "Avocado Sourdough Toast with Egg",
          quantity: 1,
          unitPrice: 10.50,
          totalPrice: 10.50,
          category: "Dining & Takeaway",
          subcategory: "Restaurants"
        },
        {
          name: "Gluten-Free Butter Croissant",
          fullName: "Gluten-Free Butter Croissant",
          quantity: 1,
          unitPrice: 3.00,
          totalPrice: 3.00,
          category: "Dining",
          subcategory: "Coffee & Cafes"
        }
      ]
    }
  },
  {
    id: "gear-depot",
    name: "Apex Outdoor Gear",
    logo: "🏔️",
    totalSum: 145.00,
    currency: "$",
    merchant: "Apex Outdoor Gear",
    date: "2026-05-28",
    time: "17:10",
    data: {
      merchantName: "Apex Outdoor Gear",
      timestamp: "2026-05-28T17:10:00Z",
      totalAmount: 145.00,
      currency: "$",
      paymentMethod: "Cash",
      additionalNotes: "10% Member discount applied on apparel items • Return within 30 days.",
      items: [
        {
          name: "Ultralight Carbon Trekking Poles (Pair)",
          fullName: "Ultralight Carbon Trekking Poles (Pair)",
          quantity: 1,
          unitPrice: 85.00,
          totalPrice: 85.00,
          category: "Entertainment & Leisure",
          subcategory: "Hobbies"
        },
        {
          name: "Quick-Dry Moisture-Wicking Trail Shirt",
          fullName: "Quick-Dry Moisture-Wicking Trail Shirt",
          quantity: 2,
          unitPrice: 30.00,
          totalPrice: 60.00,
          category: "Apparel",
          subcategory: "Clothing"
        }
      ]
    }
  }
];
