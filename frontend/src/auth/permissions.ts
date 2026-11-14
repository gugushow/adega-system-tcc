import type { UserRole } from '../types/user'

/**
 * Quem pode ver cada área do sistema. É o espelho das regras do backend (Auth/Roles.cs e os
 * [Authorize] dos controllers). Esconder no frontend é só conforto: quem garante a segurança
 * é o backend, que responde 403 mesmo que alguém chame a API direto.
 */
export const access = {
  all: ['ADMIN', 'VENDEDOR', 'ESTOQUISTA'],
  sales: ['ADMIN', 'VENDEDOR'],
  customers: ['ADMIN', 'VENDEDOR'],
  stock: ['ADMIN', 'ESTOQUISTA'],
  /** Cadastrar, editar e desativar produtos e categorias (ver a lista é para todos). */
  catalogWrite: ['ADMIN', 'ESTOQUISTA'],
  admin: ['ADMIN'],
} satisfies Record<string, UserRole[]>

export const canAccess = (role: UserRole | undefined, allowed: UserRole[]) => role !== undefined && allowed.includes(role)
