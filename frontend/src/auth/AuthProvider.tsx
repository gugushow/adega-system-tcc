import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authApi } from '../api/auth'
import type { LoginInput, User, UserRole } from '../types/user'
import { AuthContext, type AuthContextValue } from './authContext'
import { authStorage } from './authStorage'
import { canAccess } from './permissions'

/**
 * Guarda quem está logado e deixa isso disponível para toda a aplicação (via useAuth).
 * Ao abrir o sistema, recupera a sessão do localStorage e confirma no backend (/auth/me)
 * que o usuário continua ativo e com o mesmo perfil.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => authStorage.load()?.user ?? null)

  useEffect(() => {
    if (!authStorage.getToken()) return
    // Se o token não valer mais, o interceptor do Axios trata o 401 e manda para o login.
    authApi
      .me()
      .then((current) => {
        authStorage.updateUser(current)
        setUser(current)
      })
      .catch(() => {})
  }, [])

  const login = useCallback(async (input: LoginInput) => {
    const session = await authApi.login(input)
    authStorage.save(session)
    setUser(session.user)
    return session.user
  }, [])

  const logout = useCallback(() => {
    authStorage.clear()
    setUser(null)
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({ user, login, logout, hasRole: (allowed: UserRole[]) => canAccess(user?.role, allowed) }),
    [user, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
