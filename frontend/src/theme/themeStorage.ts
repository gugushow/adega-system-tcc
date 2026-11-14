/** Preferência escolhida pelo usuário. "system" segue o tema do sistema operacional. */
export type ThemePreference = 'light' | 'dark' | 'system'
/** Tema que está de fato na tela. */
export type ResolvedTheme = 'light' | 'dark'

/** Mesma chave usada pelo script do index.html, que aplica o tema antes do React carregar. */
const STORAGE_KEY = 'adega.theme'

/**
 * Lê e grava a preferência no localStorage. O try/catch cobre navegadores que bloqueiam
 * o armazenamento (janela anônima de alguns navegadores): aí o tema só não é lembrado.
 */
export const themeStorage = {
  load(): ThemePreference {
    try {
      const value = localStorage.getItem(STORAGE_KEY)
      return value === 'light' || value === 'dark' ? value : 'system'
    } catch {
      return 'system'
    }
  },

  save(preference: ThemePreference) {
    try {
      if (preference === 'system') localStorage.removeItem(STORAGE_KEY)
      else localStorage.setItem(STORAGE_KEY, preference)
    } catch {
      // sem armazenamento disponível: a escolha vale até fechar a aba
    }
  },
}
