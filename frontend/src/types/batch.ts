export interface Batch {
  id: number
  productId: number
  productName: string
  batchNumber: string
  purchaseId: number | null
  supplierName: string | null
  quantityReceived: number
  quantityAvailable: number
  /** "2026-09-30" (data sem hora). */
  manufacturingDate: string | null
  /** Nulo para produtos sem validade. */
  expirationDate: string | null
  /** Dias até vencer (negativo se já venceu). Nulo sem validade. */
  daysToExpire: number | null
  isExpired: boolean
  unitCost: number
  createdAt: string
}

export type BatchStatusFilter = 'Expired' | 'Expiring'

export interface BatchQuery {
  productId?: number
  onlyAvailable?: boolean
  status?: BatchStatusFilter
  days?: number
  page?: number
  pageSize?: number
}
