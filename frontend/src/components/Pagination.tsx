import { Pagination as BsPagination } from 'react-bootstrap'

interface PaginationProps {
  page: number
  totalPages: number
  totalItems: number
  onPageChange: (page: number) => void
}

/** Paginação simples: anterior, páginas próximas da atual e próxima. */
export function Pagination({ page, totalPages, totalItems, onPageChange }: PaginationProps) {
  if (totalPages <= 1) {
    return <small className="text-secondary">{totalItems} registro(s)</small>
  }

  // Mostra no máximo 5 números ao redor da página atual.
  const first = Math.max(1, Math.min(page - 2, totalPages - 4))
  const pages = Array.from({ length: Math.min(5, totalPages) }, (_, i) => first + i)

  return (
    <div className="d-flex flex-wrap align-items-center justify-content-between gap-2">
      <small className="text-secondary">{totalItems} registro(s)</small>
      <BsPagination className="mb-0">
        <BsPagination.Prev disabled={page === 1} onClick={() => onPageChange(page - 1)}>
          <span aria-hidden="true">‹</span>
          <span className="visually-hidden">Página anterior</span>
        </BsPagination.Prev>
        {pages.map((number) => (
          <BsPagination.Item key={number} active={number === page} onClick={() => onPageChange(number)}>
            {number}
          </BsPagination.Item>
        ))}
        <BsPagination.Next disabled={page === totalPages} onClick={() => onPageChange(page + 1)}>
          <span aria-hidden="true">›</span>
          <span className="visually-hidden">Próxima página</span>
        </BsPagination.Next>
      </BsPagination>
    </div>
  )
}
