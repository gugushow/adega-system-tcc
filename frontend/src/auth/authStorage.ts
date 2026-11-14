import type { LoginResponse, User } from '../types/user'

const STORAGE_KEY = 'adega.auth'

/**
 * Guarda a sessão (token + usuário) no localStorage, para o login sobreviver a um F5.
 * Fica fora do React porque o interceptor do Axios (api/http.ts) também precisa ler o token.
 */
export const authStorage = {
  save(session: LoginResponse) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session))
  },

  /** A sessão salva, ou null se não houver ou se o token já tiver vencido. */
  load(): LoginResponse | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY)
      if (!raw) return null
      const session = JSON.parse(raw) as LoginResponse
      if (new Date(session.expiresAt).getTime() <= Date.now()) {
        localStorage.removeItem(STORAGE_KEY)
        return null
      }
      return session
    } catch {
      return null
    }
  },

  getToken: () => authStorage.load()?.token ?? null,

  /** Atualiza só os dados do usuário (ex.: depois de buscar /auth/me), mantendo o token. */
  updateUser(user: User) {
    const session = authStorage.load()
    if (session) authStorage.save({ ...session, user })
  },

  clear() {
    localStorage.removeItem(STORAGE_KEY)
  },
}
