import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { ThemeContext, type ThemeContextValue } from './themeContext'
import { themeStorage, type ResolvedTheme, type ThemePreference } from './themeStorage'

const darkQuery = '(prefers-color-scheme: dark)'

const systemTheme = (): ResolvedTheme => (window.matchMedia(darkQuery).matches ? 'dark' : 'light')

/**
 * O único lugar que cuida do tema. Fluxo:
 *
 *   preferência (claro | escuro | sistema) → localStorage
 *        ↓
 *   tema resolvido (claro | escuro)  ← sistema operacional, quando a preferência é "sistema"
 *        ↓
 *   <html data-bs-theme="dark">  → o Bootstrap e os nossos tokens (styles/tokens.css) trocam as cores
 *
 * Nenhum componente decide cor por conta própria: todos usam as variáveis CSS, que mudam sozinhas
 * quando o atributo do <html> muda. Até os gráficos recebem cores como var(--chart-...)
 * (components/charts/chartTheme.ts). O `theme` do contexto fica disponível para o seletor de tema.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(themeStorage.load)
  const [system, setSystem] = useState<ResolvedTheme>(systemTheme)

  // Acompanha o sistema operacional: com "Sistema" escolhido, trocar o tema do Windows/macOS troca aqui na hora.
  useEffect(() => {
    const media = window.matchMedia(darkQuery)
    const onChange = () => setSystem(media.matches ? 'dark' : 'light')
    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [])

  const theme = preference === 'system' ? system : preference

  useEffect(() => {
    const root = document.documentElement
    root.setAttribute('data-bs-theme', theme)
    // Barras de rolagem, campos de data e outros controles nativos do navegador acompanham o tema.
    root.style.colorScheme = theme
  }, [theme])

  const value = useMemo<ThemeContextValue>(
    () => ({
      preference,
      theme,
      setPreference: (next) => {
        themeStorage.save(next)
        setPreferenceState(next)
      },
    }),
    [preference, theme],
  )

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}
