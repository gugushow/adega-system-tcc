export interface Category {
  id: number
  name: string
  description: string | null
  active: boolean
  createdAt: string
  productCount: number
}

export interface CategoryInput {
  name: string
  description: string | null
}

export interface UpdateCategoryInput extends CategoryInput {
  active: boolean
}

export interface CategoryQuery {
  search?: string
  active?: boolean
  page?: number
  pageSize?: number
}
