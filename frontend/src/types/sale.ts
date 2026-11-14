export type PaymentMethod = 'PIX' | 'CASH' | 'CREDIT_CARD' | 'DEBIT_CARD'
export type SaleStatus = 'COMPLETED' | 'CANCELED'

export const paymentMethodLabels: Record<PaymentMethod, string> = {
  PIX: 'Pix',
  CASH: 'Dinheiro',
  CREDIT_CARD: 'Cartão de crédito',
  DEBIT_CARD: 'Cartão de débito',
}

export const saleStatusLabels: Record<SaleStatus, string> = {
  COMPLETED: 'Concluída',
  CANCELED: 'Cancelada',
}

/** Corpo do POST /api/sales. Não leva preço: a API usa o preço do cadastro do produto. */
export interface CreateSaleInput {
  customerId: number | null
  paymentMethod: PaymentMethod
  /** Desconto na venda inteira, em reais. */
  discount: number
  items: { productId: number; quantity: number; discount?: number }[]
  /** Combos vendidos. A API desmonta cada um nos produtos e baixa o estoque de cada produto. */
  combos?: { comboId: number; quantity: number }[]
}

/** Uma linha da listagem de vendas. */
export interface Sale {
  id: number
  saleDate: string
  customerId: number | null
  customerName: string | null
  userName: string
  itemCount: number
  subtotal: number
  discount: number
  total: number
  paymentMethod: PaymentMethod
  status: SaleStatus
}

export interface SaleItemBatch {
  batchId: number
  batchNumber: string
  expirationDate: string | null
  quantity: number
  unitCost: number
}

export interface SaleItem {
  id: number
  productId: number
  productName: string
  /** Preenchido quando o item veio de um combo: o desconto do item é a parte dele na economia do combo. */
  comboId: number | null
  comboName: string | null
  quantity: number
  unitPrice: number
  /** Custo médio dos lotes usados. */
  costPrice: number
  discount: number
  total: number
  batches: SaleItemBatch[]
}

export interface SaleDetails extends Omit<Sale, 'itemCount'> {
  totalCost: number
  profit: number
  canceledAt: string | null
  canceledByUserName: string | null
  cancelReason: string | null
  /** Pontos de fidelidade que a venda deu ao cliente (0 sem cliente ou com o programa desligado). */
  loyaltyPoints: number
  items: SaleItem[]
}

export interface SaleQuery {
  from?: string
  to?: string
  status?: SaleStatus
  customerId?: number
  paymentMethod?: PaymentMethod
  page?: number
  pageSize?: number
}
