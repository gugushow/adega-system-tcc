import { useEffect, useRef } from 'react'
import { Alert } from 'react-bootstrap'

export interface Feedback {
  type: 'success' | 'danger'
  message: string
}

/** Quanto tempo a mensagem de sucesso fica na tela antes de sumir sozinha. */
const SUCCESS_TIMEOUT_MS = 6000

/**
 * Mensagem de sucesso ou erro no topo da página.
 * Sucesso some sozinho depois de alguns segundos; erro fica até o usuário fechar (precisa ser lido).
 */
export function AppAlert({ feedback, onClose }: { feedback: Feedback | null; onClose: () => void }) {
  // A página costuma passar uma função nova a cada renderização; o ref guarda a mais recente
  // sem reiniciar o relógio toda vez que a página renderiza.
  const onCloseRef = useRef(onClose)
  useEffect(() => {
    onCloseRef.current = onClose
  })

  useEffect(() => {
    if (feedback?.type !== 'success') return
    const timer = setTimeout(() => onCloseRef.current(), SUCCESS_TIMEOUT_MS)
    return () => clearTimeout(timer)
  }, [feedback])

  if (!feedback) {
    return null
  }
  return (
    <Alert variant={feedback.type} dismissible closeLabel="Fechar" onClose={onClose}>
      {feedback.message}
    </Alert>
  )
}
