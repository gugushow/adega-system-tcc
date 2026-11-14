export type MovementType = 'PURCHASE' | 'SALE' | 'ADJUSTMENT' | 'LOSS' | 'EXPIRED' | 'RETURN'

/** Nome de cada tipo em português, para as telas. */
export const movementTypeLabels: Record<MovementType, string> = {
  PURCHASE: 'Compra',
  SALE: 'Venda',
  ADJUSTMENT: 'Ajuste',
  LOSS: 'Perda',
  EXPIRED: 'Vencido',
  RETURN: 'Devolução',
}

export interface StockMovement {
  id: number
  productId: number
  productName: string
  movementType: MovementType
  batchId: number | null
  batchNumber: string | null
  /** Com sinal: positivo entrou, negativo saiu. */
  quantity: number
  previousStock: number
  newStock: number
  reason: string
  referenceType: string | null
  referenceId: number | null
  userName: string
  createdAt: string
}

export interface StockMovementQuery {
  productId?: number
  type?: MovementType
  from?: string
  to?: string
  page?: number
  pageSize?: number
}

export interface StockAdjustmentInput {
  batchId: number
  type: 'ADJUSTMENT' | 'LOSS'
  quantity: number
  reason: string
}

export interface CostHistoryEntry {
  id: number
  date: string
  costPrice: number
  /** Variação (%) em relação à compra anterior. Nulo na primeira. */
  changePercent: number | null
  supplierId: number
  supplierName: string
  purchaseId: number
}

export interface CostHistory {
  productId: number
  productName: string
  currentCostPrice: number
  totalChangePercent: number | null
  entries: CostHistoryEntry[]
}
