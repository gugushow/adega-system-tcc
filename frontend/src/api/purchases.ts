import type { PagedResult } from '../types/api'
import type { Purchase, PurchaseDetails, PurchaseInput, PurchaseQuery } from '../types/purchase'
import { http, unwrap } from './http'

// Compras não têm update nem delete: são documentos.
export const purchasesApi = {
  list: (query: PurchaseQuery) => unwrap<PagedResult<Purchase>>(http.get('/purchases', { params: query })),
  get: (id: number) => unwrap<PurchaseDetails>(http.get(`/purchases/${id}`)),
  create: (input: PurchaseInput) => unwrap<PurchaseDetails>(http.post('/purchases', input)),
}
