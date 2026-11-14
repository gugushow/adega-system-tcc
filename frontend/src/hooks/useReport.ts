import { useEffect, useState, type DependencyList } from 'react'
import { getErrorMessage } from '../api/http'

/**
 * Carrega um relatório e recarrega quando os filtros (deps) mudam.
 * Respostas que chegam fora de ordem são ignoradas: só vale a do filtro mais recente.
 */
export function useReport<T>(load: () => Promise<T>, deps: DependencyList) {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  // As dependências vêm de quem chama (os filtros); o linter não consegue conferi-las aqui.
  /* eslint-disable react-hooks/exhaustive-deps */
  useEffect(() => {
    let ignore = false
    setLoading(true)
    setError(null)
    load()
      .then((result) => !ignore && setData(result))
      .catch((err) => !ignore && setError(getErrorMessage(err)))
      .finally(() => !ignore && setLoading(false))
    return () => {
      ignore = true
    }
  }, deps)
  /* eslint-enable react-hooks/exhaustive-deps */

  return { data, error, loading }
}
