import type { PagedResult } from '../types/api'
import type { Combo, ComboInput, ComboQuery, UpdateComboInput } from '../types/combo'
import { http, unwrap } from './http'

export const combosApi = {
  list: (query: ComboQuery) => unwrap<PagedResult<Combo>>(http.get('/combos', { params: query })),
  get: (id: number) => unwrap<Combo>(http.get(`/combos/${id}`)),
  create: (input: ComboInput) => unwrap<Combo>(http.post('/combos', input)),
  update: (id: number, input: UpdateComboInput) => unwrap<Combo>(http.put(`/combos/${id}`, input)),
  deactivate: (id: number) => http.delete(`/combos/${id}`),
}
