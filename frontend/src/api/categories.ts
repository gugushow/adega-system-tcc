import type { PagedResult } from '../types/api'
import type { Category, CategoryInput, CategoryQuery, UpdateCategoryInput } from '../types/category'
import { http, unwrap } from './http'

// Uma função por endpoint de /api/categories.
export const categoriesApi = {
  list: (query: CategoryQuery) => unwrap<PagedResult<Category>>(http.get('/categories', { params: query })),
  /** Categorias ativas para preencher selects (são poucas, cabem numa página). */
  listActive: () =>
    unwrap<PagedResult<Category>>(http.get('/categories', { params: { active: true, pageSize: 100 } })).then(
      (result) => result.items,
    ),
  create: (input: CategoryInput) => unwrap<Category>(http.post('/categories', input)),
  update: (id: number, input: UpdateCategoryInput) => unwrap<Category>(http.put(`/categories/${id}`, input)),
  deactivate: (id: number) => http.delete(`/categories/${id}`),
}
