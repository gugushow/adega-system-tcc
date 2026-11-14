import type { PagedResult } from '../types/api'
import type { CreateSaleInput, Sale, SaleDetails, SaleQuery } from '../types/sale'
import { http, unwrap } from './http'

export const salesApi = {
  list: (query: SaleQuery) => unwrap<PagedResult<Sale>>(http.get('/sales', { params: query })),
  get: (id: number) => unwrap<SaleDetails>(http.get(`/sales/${id}`)),
  create: (input: CreateSaleInput) => unwrap<SaleDetails>(http.post('/sales', input)),
  cancel: (id: number, reason: string) => unwrap<SaleDetails>(http.post(`/sales/${id}/cancel`, { reason })),
}
