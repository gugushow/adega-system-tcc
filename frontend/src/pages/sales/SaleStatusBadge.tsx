import { Badge } from 'react-bootstrap'
import { saleStatusLabels, type SaleStatus } from '../../types/sale'

export function SaleStatusBadge({ status }: { status: SaleStatus }) {
  return <Badge bg={status === 'COMPLETED' ? 'success' : 'secondary'}>{saleStatusLabels[status]}</Badge>
}
