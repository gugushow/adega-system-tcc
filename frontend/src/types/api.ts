// Espelho dos tipos de resposta da API (backend/src/AdegaSystem.Api/Common).

export interface ApiError {
  field: string | null
  message: string
}

export interface ApiResponse<T> {
  success: boolean
  message: string | null
  data: T | null
  errors: ApiError[]
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}
