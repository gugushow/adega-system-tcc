import { Link } from 'react-router'
import { EmptyState } from '../components/EmptyState'

/** Endereço que não existe (link antigo ou digitado errado). */
export function NotFoundPage() {
  return (
    <EmptyState
      icon="bi-signpost-split"
      title="Página não encontrada"
      description="O endereço digitado não existe ou mudou de lugar."
      action={
        <Link to="/" className="btn btn-primary">
          Voltar ao início
        </Link>
      }
    />
  )
}
