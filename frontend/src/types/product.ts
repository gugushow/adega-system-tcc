export interface Product {
  id: number
  name: string
  description: string | null
  barcode: string | null
  brand: string | null
  categoryId: number
  categoryName: string
  volume: string | null
  alcoholContent: number | null
  costPrice: number
  salePrice: number
  marginPercent: number
  minimumStock: number
  currentStock: number
  isLowStock: boolean
  isOutOfStock: boolean
  active: boolean
  createdAt: string
  updatedAt: string | null
}

// Não existe currentStock aqui: o estoque nunca é editado pelo cadastro do produto.
export interface ProductInput {
  name: string
  description: string | null
  barcode: string | null
  brand: string | null
  categoryId: number
  volume: string | null
  alcoholContent: number | null
  costPrice: number
  salePrice: number
  minimumStock: number
}

export interface UpdateProductInput extends ProductInput {
  active: boolean
}

export type StockFilter = 'Low' | 'Out'

export interface ProductQuery {
  search?: string
  categoryId?: number
  active?: boolean
  stock?: StockFilter
  page?: number
  pageSize?: number
}
