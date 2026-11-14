import { Spinner } from 'react-bootstrap'

export function Loading({ text = 'Carregando...' }: { text?: string }) {
  return (
    <div className="d-flex align-items-center justify-content-center gap-2 py-5 text-secondary" role="status">
      <Spinner animation="border" size="sm" aria-hidden="true" />
      <span>{text}</span>
    </div>
  )
}
