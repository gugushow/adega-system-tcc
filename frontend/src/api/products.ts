import type { PagedResult } from '../types/api'
import type { Product, ProductInput, ProductQuery, UpdateProductInput } from '../types/product'
import { http, unwrap } from './http'

// Uma função por endpoint de /api/products.
export const productsApi = {
  list: (query: ProductQuery) => unwrap<PagedResult<Product>>(http.get('/products', { params: query })),
  getByBarcode: (barcode: string) => unwrap<Product>(http.get(`/products/barcode/${encodeURIComponent(barcode)}`)),
  create: (input: ProductInput) => unwrap<Product>(http.post('/products', input)),
  update: (id: number, input: UpdateProductInput) => unwrap<Product>(http.put(`/products/${id}`, input)),
  deactivate: (id: number) => http.delete(`/products/${id}`),
}
