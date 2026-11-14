import { createContext } from 'react'
import type { ResolvedTheme, ThemePreference } from './themeStorage'

export interface ThemeContextValue {
  /** O que o usuário escolheu: claro, escuro ou seguir o sistema. */
  preference: ThemePreference
  /** O tema aplicado agora (com "system" já resolvido para claro ou escuro). */
  theme: ResolvedTheme
  setPreference: (preference: ThemePreference) => void
}

export const ThemeContext = createContext<ThemeContextValue | null>(null)
