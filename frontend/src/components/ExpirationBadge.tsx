import { Badge } from 'react-bootstrap'
import { formatDate } from '../utils/format'

interface ExpirationBadgeProps {
  expirationDate: string | null
  daysToExpire: number | null
}

/**
 * Validade com destaque: vermelho vencido ou vencendo em até 7 dias,
 * amarelo em até 30 dias, cinza sem validade. O texto sempre diz a situação (não depende só da cor). Os dias vêm calculados pela API.
 */
export function ExpirationBadge({ expirationDate, daysToExpire }: ExpirationBadgeProps) {
  if (!expirationDate || daysToExpire === null) {
    return <Badge bg="secondary">Sem validade</Badge>
  }

  const date = formatDate(expirationDate)
  if (daysToExpire < 0) {
    return <Badge bg="danger">{date} · vencido</Badge>
  }
  if (daysToExpire === 0) {
    return <Badge bg="danger">{date} · vence hoje</Badge>
  }
  if (daysToExpire <= 7) {
    return <Badge bg="danger">{date} · {daysToExpire} dia(s)</Badge>
  }
  if (daysToExpire <= 30) {
    return (
      <Badge bg="warning">
        {date} · {daysToExpire} dias
      </Badge>
    )
  }
  return (
    <Badge bg="neutral">{date}</Badge>
  )
}
