import { useContext } from 'react'
import { ThemeContext } from './themeContext'

/** Tema atual e a função para trocá-lo, em qualquer componente. */
export function useTheme() {
  const context = useContext(ThemeContext)
  if (!context) {
    throw new Error('useTheme precisa estar dentro de <ThemeProvider>.')
  }
  return context
}
