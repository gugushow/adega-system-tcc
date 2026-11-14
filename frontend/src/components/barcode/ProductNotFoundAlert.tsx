import { Alert, Button } from 'react-bootstrap'
import { access } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'

interface ProductNotFoundAlertProps {
  code: string
  /** Botão "Cadastrar produto". Só aparece para quem pode cadastrar produtos (ADMIN e ESTOQUISTA). */
  onCreateProduct?: (code: string) => void
  onClose?: () => void
}

/** "Produto não encontrado." depois de ler um código que não está cadastrado (PDV, Produtos, consulta rápida). */
export function ProductNotFoundAlert({ code, onCreateProduct, onClose }: ProductNotFoundAlertProps) {
  const { hasRole } = useAuth()
  const canCreate = onCreateProduct !== undefined && hasRole(access.catalogWrite)
  return (
    <Alert
      variant="warning"
      dismissible={onClose !== undefined}
      closeLabel="Fechar"
      onClose={onClose}
      className="py-2 d-flex flex-wrap align-items-center gap-2"
      role="alert"
    >
      <span>
        <strong>Produto não encontrado.</strong> Nenhum produto cadastrado com o código{' '}
        <span className="font-monospace">{code}</span>.
      </span>
      {canCreate && (
        <Button size="sm" variant="outline-secondary" onClick={() => onCreateProduct(code)}>
          <i className="bi bi-plus-lg me-1" aria-hidden="true" />
          Cadastrar produto
        </Button>
      )}
    </Alert>
  )
}
