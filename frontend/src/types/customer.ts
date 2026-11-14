export interface Customer {
  id: number
  name: string
  /** Só os 11 dígitos, sem máscara. */
  cpf: string | null
  phone: string | null
  email: string | null
  /** "1990-05-20" (data sem hora). */
  birthDate: string | null
  active: boolean
  createdAt: string
}

export interface CustomerInput {
  name: string
  cpf: string | null
  phone: string | null
  email: string | null
  birthDate: string | null
}

export interface UpdateCustomerInput extends CustomerInput {
  active: boolean
}

export interface CustomerQuery {
  search?: string
  active?: boolean
  page?: number
  pageSize?: number
}

/** Só vendas concluídas entram no resumo. */
export interface CustomerSummary {
  customerId: number
  customerName: string
  purchaseCount: number
  totalSpent: number
  /** Data e hora UTC da última compra. */
  lastPurchaseDate: string | null
  averageTicket: number
}
