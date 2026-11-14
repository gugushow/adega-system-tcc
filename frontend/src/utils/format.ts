const currency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
// Custo unitário de compra aceita até 4 casas (ex.: R$ 3,8975 de uma caixa com desconto).
const unitCost = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', maximumFractionDigits: 4 })
const integer = new Intl.NumberFormat('pt-BR')
const percent = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export const formatCurrency = (value: number) => currency.format(value)
/** Custo unitário de compra, com até 4 casas: "R$ 3,8975". */
export const formatUnitCost = (value: number) => unitCost.format(value)
/** Contagens com separador de milhar: 12345 → "12.345". */
export const formatNumber = (value: number) => integer.format(value)
export const formatPercent = (value: number) => `${percent.format(value)}%`

/** "" vira null, para não mandar strings vazias para a API. */
export const emptyToNull = (value: string) => (value.trim() === '' ? null : value.trim())

/** Data sem hora vinda da API ("2026-09-30") → "30/09/2026". Não usa new Date() para não mudar de dia pelo fuso. */
export const formatDate = (isoDate: string) => isoDate.split('-').reverse().join('/')

/** Data e hora UTC vinda da API → horário local do navegador ("01/10/2026, 18:30"). */
export const formatDateTime = (isoDateTime: string) =>
  new Date(isoDateTime).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

/** Hoje no fuso do navegador, no formato do <input type="date"> ("2026-10-01"). */
export function todayIso() {
  const now = new Date()
  return new Date(now.getTime() - now.getTimezoneOffset() * 60_000).toISOString().slice(0, 10)
}

/** "12ABC34501DE35" → "12.ABC.345/01DE-35". */
export const formatCnpj = (cnpj: string) =>
  cnpj.length === 14 ? `${cnpj.slice(0, 2)}.${cnpj.slice(2, 5)}.${cnpj.slice(5, 8)}/${cnpj.slice(8, 12)}-${cnpj.slice(12)}` : cnpj

/** Aceita "3,90" e "3.90". */
export const parseDecimal = (value: string) => Number(value.replace(',', '.'))

/** "52998224725" → "529.982.247-25". */
export const formatCpf = (cpf: string) =>
  cpf.length === 11 ? `${cpf.slice(0, 3)}.${cpf.slice(3, 6)}.${cpf.slice(6, 9)}-${cpf.slice(9)}` : cpf

/** Soma dias a uma data "2026-10-01" sem passar pelo fuso do navegador. */
export function addDaysIso(isoDate: string, days: number) {
  const date = new Date(`${isoDate}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}

const shortMonths = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez']

/** "2026-10-01" → "out/26" (eixo dos gráficos mensais). */
export const formatMonth = (isoDate: string) => `${shortMonths[Number(isoDate.slice(5, 7)) - 1]}/${isoDate.slice(2, 4)}`

/** "2026-10-01" → "01/10" (eixo dos gráficos diários). */
export const formatDayMonth = (isoDate: string) => `${isoDate.slice(8, 10)}/${isoDate.slice(5, 7)}`

const compactCurrency = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  notation: 'compact',
  maximumFractionDigits: 1,
})

/** R$ 1,2 mil: para eixos de gráfico, onde o valor exato fica no tooltip. */
export const formatCurrencyCompact = (value: number) => compactCurrency.format(value)
