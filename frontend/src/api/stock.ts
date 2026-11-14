import type { PagedResult } from '../types/api'
import type { CostHistory, StockAdjustmentInput, StockMovement, StockMovementQuery } from '../types/stock'
import { http, unwrap } from './http'

export const stockApi = {
  movements: (query: StockMovementQuery) =>
    unwrap<PagedResult<StockMovement>>(http.get('/stock-movements', { params: query })),
  adjust: (input: StockAdjustmentInput) => unwrap<StockMovement>(http.post('/stock/adjustments', input)),
  costHistory: (productId: number) => unwrap<CostHistory>(http.get(`/products/${productId}/cost-history`)),
}
