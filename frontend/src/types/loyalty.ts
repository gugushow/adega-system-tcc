import type { PagedResult } from './api'

export type LoyaltyType = 'EARN' | 'REDEEM' | 'ADJUSTMENT'

export const loyaltyTypeLabels: Record<LoyaltyType, string> = {
  EARN: 'Ganho',
  REDEEM: 'Resgate',
  ADJUSTMENT: 'Ajuste',
}

export interface LoyaltyTransaction {
  id: number
  createdAt: string
  type: LoyaltyType
  /** Positivo soma ao saldo, negativo tira. */
  points: number
  description: string
  saleId: number | null
  userName: string
}

/** Saldo do cliente, a regra atual do programa e uma página do extrato. */
export interface LoyaltyStatement {
  customerId: number
  customerName: string
  balance: number
  programEnabled: boolean
  amountPerPoint: number
  transactions: PagedResult<LoyaltyTransaction>
}

export interface LoyaltySettings {
  enabled: boolean
  /** Quantos reais de compra valem 1 ponto. */
  amountPerPoint: number
}
