import type { PagedResult } from '../types/api'
import type { Batch, BatchQuery } from '../types/batch'
import { http, unwrap } from './http'

export const batchesApi = {
  list: (query: BatchQuery) => unwrap<PagedResult<Batch>>(http.get('/batches', { params: query })),
  /** Lotes de um produto para escolher num select (até 100, ordenados pela validade). */
  listByProduct: async (productId: number, onlyAvailable = true) =>
    (await batchesApi.list({ productId, onlyAvailable, pageSize: 100 })).items,
  writeOff: (id: number, reason: string | null) => unwrap<Batch>(http.post(`/batches/${id}/write-off`, { reason })),
}
