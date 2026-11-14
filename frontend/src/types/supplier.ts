export interface Supplier {
  id: number
  name: string
  corporateName: string | null
  /** Sem máscara, 14 caracteres (pode ter letras: CNPJ alfanumérico). */
  cnpj: string | null
  phone: string | null
  email: string | null
  address: string | null
  active: boolean
  createdAt: string
}

export interface SupplierInput {
  name: string
  corporateName: string | null
  cnpj: string | null
  phone: string | null
  email: string | null
  address: string | null
}

export interface UpdateSupplierInput extends SupplierInput {
  active: boolean
}

export interface SupplierQuery {
  search?: string
  active?: boolean
  page?: number
  pageSize?: number
}

export interface SupplierTopProduct {
  productId: number
  productName: string
  totalQuantity: number
  totalAmount: number
}

export interface SupplierSummary {
  supplierId: number
  supplierName: string
  purchaseCount: number
  totalPurchased: number
  /** "2026-09-30" (data sem hora). */
  lastPurchaseDate: string | null
  topProducts: SupplierTopProduct[]
}
