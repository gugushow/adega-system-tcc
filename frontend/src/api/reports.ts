import type {
  CategorySales,
  Dashboard,
  IdleProduct,
  MarginReport,
  Period,
  PeriodReport,
  ProductRankingOrder,
  ProductSales,
  ReportGroupBy,
  SalesReport,
  StockReport,
  SupplierPurchases,
} from '../types/report'
import { http, unwrap } from './http'

export const dashboardApi = {
  get: () => unwrap<Dashboard>(http.get('/dashboard')),
}

export const reportsApi = {
  sales: (period: Period, groupBy: ReportGroupBy) =>
    unwrap<SalesReport>(http.get('/reports/sales', { params: { ...period, groupBy } })),
  products: (period: Period, order: ProductRankingOrder, limit = 50) =>
    unwrap<PeriodReport<ProductSales>>(http.get('/reports/products', { params: { ...period, order, limit } })),
  categories: (period: Period) => unwrap<PeriodReport<CategorySales>>(http.get('/reports/categories', { params: period })),
  idleProducts: (days: number) => unwrap<IdleProduct[]>(http.get('/reports/idle-products', { params: { days } })),
  margins: (onlyBelowMinimum: boolean) =>
    unwrap<MarginReport>(http.get('/reports/margins', { params: { onlyBelowMinimum } })),
  purchasesBySupplier: (period: Period) =>
    unwrap<PeriodReport<SupplierPurchases>>(http.get('/reports/purchases-by-supplier', { params: period })),
  stock: () => unwrap<StockReport>(http.get('/reports/stock')),
}
