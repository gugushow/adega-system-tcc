export type AuditAction =
  | 'STOCK_ADJUSTED'
  | 'BATCH_WRITTEN_OFF'
  | 'SALE_CANCELED'
  | 'PRODUCT_PRICE_CHANGED'
  | 'PRODUCT_DEACTIVATED'
  | 'PRODUCT_REACTIVATED'
  | 'USER_CREATED'
  | 'USER_UPDATED'
  | 'USER_DEACTIVATED'
  | 'USER_PASSWORD_CHANGED'
  | 'COMBO_CREATED'
  | 'COMBO_UPDATED'
  | 'COMBO_DEACTIVATED'
  | 'LOYALTY_REDEEMED'
  | 'LOYALTY_ADJUSTED'
  | 'SETTINGS_UPDATED'

export const auditActionLabels: Record<AuditAction, string> = {
  STOCK_ADJUSTED: 'Ajuste de estoque',
  BATCH_WRITTEN_OFF: 'Baixa de lote vencido',
  SALE_CANCELED: 'Venda cancelada',
  PRODUCT_PRICE_CHANGED: 'Preço alterado',
  PRODUCT_DEACTIVATED: 'Produto desativado',
  PRODUCT_REACTIVATED: 'Produto reativado',
  USER_CREATED: 'Usuário cadastrado',
  USER_UPDATED: 'Usuário alterado',
  USER_DEACTIVATED: 'Usuário desativado',
  USER_PASSWORD_CHANGED: 'Senha alterada',
  COMBO_CREATED: 'Combo cadastrado',
  COMBO_UPDATED: 'Combo alterado',
  COMBO_DEACTIVATED: 'Combo desativado',
  LOYALTY_REDEEMED: 'Resgate de pontos',
  LOYALTY_ADJUSTED: 'Ajuste de pontos',
  SETTINGS_UPDATED: 'Configuração alterada',
}

/** Nome da tabela gravado em "entity" → como aparece na tela. */
export const auditEntityLabels: Record<string, string> = {
  Product: 'Produto',
  ProductBatch: 'Lote',
  Sale: 'Venda',
  User: 'Usuário',
  Combo: 'Combo',
  Customer: 'Cliente',
  SystemSetting: 'Configuração',
}

export interface AuditLog {
  id: number
  createdAt: string
  userId: number
  userName: string
  action: AuditAction
  entity: string
  entityId: number
  /** Nome legível do que mudou ("Heineken 350ml", "Venda #12"). */
  description: string
  /** JSON com os valores antes e depois da mudança. */
  oldValue: string | null
  newValue: string | null
}

export interface AuditLogQuery {
  entity?: string
  entityId?: number
  action?: AuditAction
  userId?: number
  from?: string
  to?: string
  page?: number
  pageSize?: number
}
