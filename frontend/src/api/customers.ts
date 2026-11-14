import type { PagedResult } from '../types/api'
import type { Customer, CustomerInput, CustomerQuery, CustomerSummary, UpdateCustomerInput } from '../types/customer'
import type { LoyaltyStatement } from '../types/loyalty'
import type { Sale, SaleQuery } from '../types/sale'
import { http, unwrap } from './http'

export const customersApi = {
  list: (query: CustomerQuery) => unwrap<PagedResult<Customer>>(http.get('/customers', { params: query })),
  summary: (id: number) => unwrap<CustomerSummary>(http.get(`/customers/${id}/summary`)),
  sales: (id: number, query: SaleQuery) => unwrap<PagedResult<Sale>>(http.get(`/customers/${id}/sales`, { params: query })),
  create: (input: CustomerInput) => unwrap<Customer>(http.post('/customers', input)),
  update: (id: number, input: UpdateCustomerInput) => unwrap<Customer>(http.put(`/customers/${id}`, input)),
  deactivate: (id: number) => http.delete(`/customers/${id}`),

  // Fidelidade: o saldo é sempre a soma do extrato, calculada pela API.
  loyalty: (id: number, query: { page?: number; pageSize?: number } = {}) =>
    unwrap<LoyaltyStatement>(http.get(`/customers/${id}/loyalty`, { params: query })),
  redeemPoints: (id: number, input: { points: number; description: string }) =>
    unwrap<LoyaltyStatement>(http.post(`/customers/${id}/loyalty/redeem`, input)),
  adjustPoints: (id: number, input: { points: number; reason: string }) =>
    unwrap<LoyaltyStatement>(http.post(`/customers/${id}/loyalty/adjust`, input)),
}
