import { useContext } from 'react'
import { AuthContext } from './authContext'

/** Acesso ao usuário logado e às funções de login/logout em qualquer componente. */
export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth precisa estar dentro de <AuthProvider>.')
  }
  return context
}
