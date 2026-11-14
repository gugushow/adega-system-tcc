// Espelho dos DTOs de relatórios e dashboard (backend/src/AdegaSystem.Api/DTOs/Reports e DTOs/Dashboard).
import type { PaymentMethod } from './sale'

/** Datas sem hora no formato "2026-10-01" (dias no horário de Brasília). */
export interface Period {
  from: string
  to: string
}

export type ReportGroupBy = 'Day' | 'Month'
export type ProductRankingOrder = 'MostSold' | 'LeastSold'

export interface PeriodReport<T> extends Period {
  items: T[]
}

export interface SalesSummary {
  salesCount: number
  /** Faturamento: soma dos totais das vendas concluídas (já com desconto). */
  revenue: number
  discounts: number
  /** Custo das mercadorias vendidas (CMV). */
  cost: number
  profit: number
  averageTicket: number
  marginPercent: number
}

export interface SalesPeriod {
  /** O dia, ou o dia 1 do mês quando agrupado por mês. */
  period: string
  salesCount: number
  revenue: number
  cost: number
  profit: number
  marginPercent: number
}

export interface PaymentMethodSales {
  paymentMethod: PaymentMethod
  salesCount: number
  revenue: number
  percent: number
}

export interface SalesReport extends Period {
  groupBy: ReportGroupBy
  summary: SalesSummary
  periods: SalesPeriod[]
  paymentMethods: PaymentMethodSales[]
}

export interface ProductSales {
  productId: number
  productName: string
  categoryName: string
  quantity: number
  revenue: number
  cost: number
  profit: number
  marginPercent: number
}

export interface CategorySales {
  categoryId: number
  categoryName: string
  quantity: number
  revenue: number
  profit: number
  percent: number
}

export interface IdleProduct {
  productId: number
  productName: string
  categoryName: string
  currentStock: number
  /** Estoque × custo: o dinheiro parado na prateleira. */
  stockCostValue: number
  /** Data e hora UTC da última venda; null = nunca vendido. */
  lastSaleDate: string | null
  daysWithoutSale: number
}

export interface MarginProduct {
  productId: number
  productName: string
  categoryName: string
  costPrice: number
  salePrice: number
  marginPercent: number
  belowMinimum: boolean
}

export interface MarginReport {
  minimumMarginPercent: number
  belowMinimumCount: number
  items: MarginProduct[]
}

export interface SupplierPurchases {
  supplierId: number
  supplierName: string
  purchaseCount: number
  totalAmount: number
  lastPurchaseDate: string
  percent: number
}

export interface StockCategory {
  categoryId: number
  categoryName: string
  productCount: number
  units: number
  costValue: number
  saleValue: number
}

export interface StockReport {
  productCount: number
  units: number
  costValue: number
  saleValue: number
  categories: StockCategory[]
}

export interface DashboardCards {
  salesToday: number
  revenueToday: number
  revenueMonth: number
  profitMonth: number
  activeProducts: number
  totalItemsInStock: number
  lowStockProducts: number
  outOfStockProducts: number
  expiringProducts: number
  expiredProducts: number
  expiringDays: number
}

export interface Dashboard {
  cards: DashboardCards
  last30Days: SalesPeriod[]
  last12Months: SalesPeriod[]
  topProducts: ProductSales[]
  topCategories: CategorySales[]
  paymentMethodsMonth: PaymentMethodSales[]
}
