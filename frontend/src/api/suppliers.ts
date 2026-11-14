import type { PagedResult } from '../types/api'
import type { Purchase, PurchaseQuery } from '../types/purchase'
import type { Supplier, SupplierInput, SupplierQuery, SupplierSummary, UpdateSupplierInput } from '../types/supplier'
import { http, unwrap } from './http'

export const suppliersApi = {
  list: (query: SupplierQuery) => unwrap<PagedResult<Supplier>>(http.get('/suppliers', { params: query })),
  /** Fornecedores ativos para selects (até 100, o máximo por página). */
  listActive: async () => (await suppliersApi.list({ active: true, pageSize: 100 })).items,
  summary: (id: number) => unwrap<SupplierSummary>(http.get(`/suppliers/${id}/summary`)),
  purchases: (id: number, query: PurchaseQuery) =>
    unwrap<PagedResult<Purchase>>(http.get(`/suppliers/${id}/purchases`, { params: query })),
  create: (input: SupplierInput) => unwrap<Supplier>(http.post('/suppliers', input)),
  update: (id: number, input: UpdateSupplierInput) => unwrap<Supplier>(http.put(`/suppliers/${id}`, input)),
  deactivate: (id: number) => http.delete(`/suppliers/${id}`),
}
