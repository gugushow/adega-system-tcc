/** Um produto dentro do combo, com o preço avulso atual. */
export interface ComboItem {
  productId: number
  productName: string
  quantity: number
  /** Preço de venda do produto sozinho (o avulso). */
  unitPrice: number
  currentStock: number
  productActive: boolean
}

export interface Combo {
  id: number
  name: string
  description: string | null
  /** Quanto o cliente paga pelo combo. */
  salePrice: number
  /** Quanto custariam os mesmos produtos comprados separados. */
  normalPrice: number
  /** normalPrice − salePrice. */
  savings: number
  savingsPercent: number
  /** Quantos combos dá para montar com o estoque atual (o produto mais escasso manda). */
  availableQuantity: number
  active: boolean
  createdAt: string
  updatedAt: string | null
  items: ComboItem[]
}

export interface ComboInput {
  name: string
  description: string | null
  salePrice: number
  items: { productId: number; quantity: number }[]
}

export interface UpdateComboInput extends ComboInput {
  active: boolean
}

export interface ComboQuery {
  search?: string
  active?: boolean
  page?: number
  pageSize?: number
}
