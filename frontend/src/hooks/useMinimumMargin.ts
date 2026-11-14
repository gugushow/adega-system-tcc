import { useEffect, useState } from 'react'
import { settingsApi } from '../api/settings'

/**
 * Margem mínima configurada (%), para as telas mostrarem o alerta "Margem abaixo do mínimo configurado.".
 * null enquanto carrega (ou se a API falhar: aí o alerta simplesmente não aparece).
 */
export function useMinimumMargin() {
  const [minimum, setMinimum] = useState<number | null>(null)

  useEffect(() => {
    let ignore = false
    settingsApi
      .getMargin()
      .then((settings) => !ignore && setMinimum(settings.minimumMarginPercent))
      .catch(() => {})
    return () => {
      ignore = true
    }
  }, [])

  return minimum
}
