import { movementTypeLabels, type MovementType } from '../../types/stock'
import { roleLabels, type UserRole } from '../../types/user'
import { saleStatusLabels, type SaleStatus } from '../../types/sale'
import { formatCurrency } from '../../utils/format'

/** Nome de cada campo gravado no JSON da auditoria → como aparece na tela. */
const fieldLabels: Record<string, string> = {
  stock: 'Estoque do produto',
  batchQuantity: 'Quantidade no lote',
  batch: 'Lote',
  type: 'Tipo',
  reason: 'Motivo',
  status: 'Situação',
  total: 'Total',
  costPrice: 'Preço de custo',
  salePrice: 'Preço de venda',
  active: 'Ativo',
  name: 'Nome',
  email: 'E-mail',
  role: 'Perfil',
  passwordReset: 'Senha redefinida',
  items: 'Produtos',
  balance: 'Saldo de pontos',
  redeemed: 'Pontos resgatados',
  description: 'Descrição',
  enabled: 'Programa ligado',
  amountPerPoint: 'Valor por ponto',
  minimumMarginPercent: 'Margem mínima',
}

const moneyFields = new Set(['total', 'costPrice', 'salePrice', 'amountPerPoint'])

function formatValue(field: string, value: unknown): string {
  if (value === undefined || value === null || value === '') return '—'
  if (typeof value === 'boolean') return value ? 'Sim' : 'Não'
  if (Array.isArray(value)) return value.join(', ')
  if (typeof value === 'number') {
    if (moneyFields.has(field)) return formatCurrency(value)
    if (field === 'minimumMarginPercent') return `${value.toLocaleString('pt-BR')}%`
    return value.toLocaleString('pt-BR')
  }
  const text = String(value)
  if (field === 'type' && text in movementTypeLabels) return movementTypeLabels[text as MovementType]
  if (field === 'role' && text in roleLabels) return roleLabels[text as UserRole]
  if (field === 'status' && text in saleStatusLabels) return saleStatusLabels[text as SaleStatus]
  return text
}

function parse(json: string | null): Record<string, unknown> {
  if (!json) return {}
  try {
    return JSON.parse(json) as Record<string, unknown>
  } catch {
    return {}
  }
}

export interface AuditChange {
  field: string
  label: string
  before: string | null
  after: string | null
}

/**
 * Transforma o JSON de antes e depois numa lista legível: "Preço de venda: R$ 6,00 → R$ 6,50".
 * Campos que ficaram iguais (como o nome de um combo que só mudou de preço) não aparecem.
 * "before" é null quando o campo só existe depois (ex.: o motivo de um ajuste).
 */
export function describeChanges(oldValue: string | null, newValue: string | null): AuditChange[] {
  const before = parse(oldValue)
  const after = parse(newValue)
  const fields = [...new Set([...Object.keys(before), ...Object.keys(after)])]

  return fields
    .filter((field) => JSON.stringify(before[field]) !== JSON.stringify(after[field]))
    .map((field) => ({
      field,
      label: fieldLabels[field] ?? field,
      before: field in before ? formatValue(field, before[field]) : null,
      after: field in after ? formatValue(field, after[field]) : null,
    }))
}
