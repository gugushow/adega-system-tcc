import { createContext } from 'react'
import type { LoginInput, User, UserRole } from '../types/user'

export interface AuthContextValue {
  /** Usuário logado, ou null se ninguém entrou. */
  user: User | null
  login: (input: LoginInput) => Promise<User>
  logout: () => void
  /** O usuário logado tem um destes perfis? */
  hasRole: (allowed: UserRole[]) => boolean
}

export const AuthContext = createContext<AuthContextValue | null>(null)
