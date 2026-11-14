import { useState } from 'react'
import { Button, Modal } from 'react-bootstrap'

interface ConfirmDialogProps {
  show: boolean
  title: string
  message: string
  confirmLabel?: string
  busy?: boolean
  onConfirm: () => void
  onCancel: () => void
}

/** Pergunta "tem certeza?" antes de ações como desativar um registro. */
export function ConfirmDialog({
  show,
  title,
  message,
  confirmLabel = 'Confirmar',
  busy = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  // Enquanto o modal some (animação de saída), a página já limpou o registro escolhido e a mensagem
  // viraria 'Deseja desativar "undefined"?'. Guardamos o último texto mostrado para a saída ficar limpa.
  // (ajustar o estado durante a renderização é o jeito recomendado pelo React para "lembrar" um valor de prop).
  const [shown, setShown] = useState({ title, message })
  if (show && (shown.title !== title || shown.message !== message)) {
    setShown({ title, message })
  }

  return (
    // Durante a operação (busy) o modal não fecha por Esc nem por clique fora.
    <Modal show={show} onHide={busy ? undefined : onCancel} backdrop={busy ? 'static' : true} centered>
      <Modal.Header closeButton={!busy} closeLabel="Fechar">
        <Modal.Title as="h5">{shown.title}</Modal.Title>
      </Modal.Header>
      <Modal.Body>{shown.message}</Modal.Body>
      <Modal.Footer>
        <Button variant="outline-secondary" onClick={onCancel} disabled={busy}>
          Cancelar
        </Button>
        <Button variant="danger" onClick={onConfirm} disabled={busy} autoFocus>
          {busy ? 'Aguarde...' : confirmLabel}
        </Button>
      </Modal.Footer>
    </Modal>
  )
}
