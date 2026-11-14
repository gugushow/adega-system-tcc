import { Badge } from 'react-bootstrap'
import type { Product } from '../../types/product'

/** Estoque atual com destaque: vermelho sem estoque, amarelo estoque baixo. */
export function StockBadge({ product }: { product: Product }) {
  if (product.isOutOfStock) {
    return <Badge bg="danger">Sem estoque</Badge>
  }
  if (product.isLowStock) {
    return (
      <Badge bg="warning">
        {product.currentStock} · baixo
      </Badge>
    )
  }
  return <Badge bg="neutral">{product.currentStock}</Badge>
}
