import { Button } from 'react-bootstrap'

interface ActionButtonProps {
  /** Classe do Bootstrap Icons (ex.: "bi-pencil"). */
  icon: string
  label: string
  /** outline-secondary para ver/editar, outline-danger para ações destrutivas (que sempre pedem confirmação). */
  variant?: 'outline-secondary' | 'outline-danger' | 'outline-primary'
  onClick: () => void
}

/**
 * Botão pequeno de ação numa linha de tabela (Editar, Desativar, Detalhes...).
 * Em telas largas mostra ícone e texto; nas menores só o ícone, para a coluna não ficar enorme.
 * O texto continua no aria-label e no title: o leitor de tela lê e o mouse mostra a dica.
 */
export function ActionButton({ icon, label, variant = 'outline-secondary', onClick }: ActionButtonProps) {
  return (
    <Button size="sm" variant={variant} onClick={onClick} aria-label={label} title={label} className="action-button">
      <i className={`bi ${icon}`} aria-hidden="true" />
      <span className="action-label" aria-hidden="true">
        {label}
      </span>
    </Button>
  )
}
