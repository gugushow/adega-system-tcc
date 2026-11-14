import { useEffect, useState } from 'react'

/**
 * Devolve o valor só depois que ele parar de mudar por "delay" ms.
 * Usado na busca: em vez de chamar a API a cada tecla, chamamos quando o usuário para de digitar.
 */
export function useDebounce<T>(value: T, delay = 400): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(timer)
  }, [value, delay])

  return debounced
}
