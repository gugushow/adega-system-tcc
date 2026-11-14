// Espelho dos DTOs de usuário e login (backend/src/AdegaSystem.Api/DTOs/Users e DTOs/Auth).

export type UserRole = 'ADMIN' | 'VENDEDOR' | 'ESTOQUISTA'

export const roleLabels: Record<UserRole, string> = {
  ADMIN: 'Administrador',
  VENDEDOR: 'Vendedor',
  ESTOQUISTA: 'Estoquista',
}

export interface User {
  id: number
  name: string
  email: string
  role: UserRole
  active: boolean
  createdAt: string
}

export interface LoginInput {
  email: string
  password: string
}

export interface LoginResponse {
  token: string
  /** Data e hora UTC em que o token vence. */
  expiresAt: string
  user: User
}

export interface UserInput {
  name: string
  email: string
  password: string
  role: UserRole
}

export interface UpdateUserInput {
  name: string
  email: string
  role: UserRole
  active: boolean
  /** Em branco mantém a senha atual. */
  newPassword: string | null
}

export interface ChangePasswordInput {
  currentPassword: string
  newPassword: string
}

export interface UserQuery {
  search?: string
  role?: UserRole
  active?: boolean
  page?: number
  pageSize?: number
}
