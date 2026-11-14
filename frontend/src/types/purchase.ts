export interface Purchase {
  id: number
  supplierId: number
  supplierName: string
  invoiceNumber: string | null
  /** "2026-09-30" (data sem hora). */
  purchaseDate: string
  totalAmount: number
  itemCount: number
  userName: string
  createdAt: string
}

export interface PurchaseItem {
  id: number
  productId: number
  productName: string
  quantity: number
  unitCost: number
  total: number
  batchId: number | null
  batchNumber: string | null
  manufacturingDate: string | null
  expirationDate: string | null
}

export interface PurchaseDetails extends Omit<Purchase, 'itemCount'> {
  items: PurchaseItem[]
}

// Totais não são enviados: o backend calcula. Cada item vira um lote.
export interface PurchaseItemInput {
  productId: number
  quantity: number
  unitCost: number
  /** Em branco, a API gera "COMPRA-{id}". */
  batchNumber: string | null
  manufacturingDate: string | null
  /** Em branco para produtos sem validade. */
  expirationDate: string | null
}

export interface PurchaseInput {
  supplierId: number
  invoiceNumber: string | null
  purchaseDate: string
  items: PurchaseItemInput[]
}

export interface PurchaseQuery {
  supplierId?: number
  from?: string
  to?: string
  page?: number
  pageSize?: number
}
