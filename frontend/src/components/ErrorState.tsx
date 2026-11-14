import { Button } from 'react-bootstrap'
import { EmptyState } from './EmptyState'

/** Mesma frase que o getErrorMessage (api/http.ts) usa quando a API não responde. */
const OFFLINE_PREFIX = 'Não foi possível conectar ao servidor'

interface ErrorStateProps {
  message: string
  /** Quando informado, mostra o botão "Tentar de novo". */
  onRetry?: () => void
}

/**
 * Erro ao carregar uma tela inteira (dashboard, relatório). Servidor fora do ar ganha título e
 * ícone próprios, para o usuário saber que o problema é a conexão e não o que ele fez.
 */
export function ErrorState({ message, onRetry }: ErrorStateProps) {
  const offline = message.startsWith(OFFLINE_PREFIX)
  return (
    <div role="alert">
      <EmptyState
        icon={offline ? 'bi-wifi-off' : 'bi-exclamation-octagon'}
        title={offline ? 'Servidor indisponível' : 'Não foi possível carregar'}
        description={message}
        action={
          onRetry && (
            <Button variant="outline-secondary" onClick={onRetry}>
              <i className="bi bi-arrow-clockwise me-1" aria-hidden="true" />
              Tentar de novo
            </Button>
          )
        }
      />
    </div>
  )
}
