import type { ReactNode } from 'react'
import { ErrorState } from '../../components/ErrorState'
import { Loading } from '../../components/Loading'

/** Mostra o carregando/erro de um relatório e, quando os dados chegam, o conteúdo. */
export function ReportState<T>({
  data,
  error,
  loading,
  children,
}: {
  data: T | null
  error: string | null
  loading: boolean
  children: (data: T) => ReactNode
}) {
  if (error) return <ErrorState message={error} />
  if (!data) return loading ? <Loading /> : null
  // Ao trocar o filtro, os dados antigos continuam na tela (meio apagados) até os novos chegarem.
  return <div style={{ opacity: loading ? 0.5 : 1, transition: 'opacity 0.2s' }}>{children(data)}</div>
}
