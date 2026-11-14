import { useEffect, useState } from 'react'
import { Alert, Badge, Button, Modal, Spinner } from 'react-bootstrap'
import { Link } from 'react-router'
import { batchesApi } from '../../api/batches'
import { getErrorMessage } from '../../api/http'
import { access } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { Batch } from '../../types/batch'
import type { Product } from '../../types/product'
import { formatCurrency, formatNumber } from '../../utils/format'
import { ExpirationBadge } from '../ExpirationBadge'
import type { ProductLookupResult } from './productLookup'
import { ProductNotFoundAlert } from './ProductNotFoundAlert'
import './ProductQuickView.css'

/** Quantos lotes aparecem na consulta rápida (os que vencem primeiro). */
const BATCHES_SHOWN = 3

interface ProductQuickViewModalProps {
  result: ProductLookupResult
  onClose: () => void
  /** Fecha e abre o leitor de novo, para consultar outro produto. */
  onScanAgain: () => void
  /** "Cadastrar produto" quando o código não existe (só aparece para quem pode cadastrar). */
  onCreateProduct: (code: string) => void
}

/**
 * Consulta rápida de um produto lido pela câmera: preço e estoque em destaque, sem custo nem margem
 * (para nenhum perfil). Lotes e validade só para quem já vê lotes no sistema (ADMIN e ESTOQUISTA).
 */
export function ProductQuickViewModal({ result, onClose, onScanAgain, onCreateProduct }: ProductQuickViewModalProps) {
  const title = result.kind === 'found' ? result.product.name : 'Consulta de produto'
  return (
    <Modal show onHide={onClose} centered fullscreen="sm-down" aria-labelledby="quick-view-title">
      <Modal.Header closeButton closeLabel="Fechar">
        <Modal.Title as="h5" id="quick-view-title">
          {title}
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {result.kind === 'found' && <ProductDetails product={result.product} />}
        {result.kind === 'not-found' && <ProductNotFoundAlert code={result.code} onCreateProduct={onCreateProduct} />}
        {result.kind === 'error' && (
          <Alert variant="danger" className="py-2 mb-0">
            {result.message}
          </Alert>
        )}
      </Modal.Body>
      <Modal.Footer>
        <Button variant="outline-secondary" onClick={onScanAgain}>
          <i className="bi bi-upc-scan me-1" aria-hidden="true" />
          Consultar outro
        </Button>
        <Button onClick={onClose}>Fechar</Button>
      </Modal.Footer>
    </Modal>
  )
}

function stockStatus(product: Product) {
  if (product.isOutOfStock) return <Badge bg="danger">Sem estoque</Badge>
  if (product.isLowStock) return <Badge bg="warning">Estoque baixo</Badge>
  return <Badge bg="success">Estoque normal</Badge>
}

function ProductDetails({ product }: { product: Product }) {
  const { hasRole } = useAuth()
  const subtitle = [product.brand, product.volume].filter(Boolean).join(' · ')
  return (
    <>
      {subtitle && <p className="text-secondary mb-3">{subtitle}</p>}
      {!product.active && (
        <Alert variant="warning" className="py-2">
          Produto inativo: não aparece no PDV e não pode ser vendido.
        </Alert>
      )}

      <div className="quick-view-highlights mb-3">
        <div className="quick-view-highlight">
          <span className="quick-view-label">Preço de venda</span>
          <span className="quick-view-value">{formatCurrency(product.salePrice)}</span>
        </div>
        <div className="quick-view-highlight">
          <span className="quick-view-label">Estoque atual</span>
          <span className="quick-view-value">{formatNumber(product.currentStock)}</span>
          {stockStatus(product)}
        </div>
      </div>

      <dl className="quick-view-details mb-0">
        <dt>Categoria</dt>
        <dd>{product.categoryName}</dd>
        <dt>Código de barras</dt>
        <dd className="font-monospace">{product.barcode ?? '—'}</dd>
        <dt>Estoque mínimo</dt>
        <dd>{formatNumber(product.minimumStock)}</dd>
      </dl>

      {/* Lotes: a API só libera para ADMIN e ESTOQUISTA (o VENDEDOR não vê lotes em nenhuma tela). */}
      {hasRole(access.stock) && <NextBatches productId={product.id} />}
    </>
  )
}

function NextBatches({ productId }: { productId: number }) {
  const [batches, setBatches] = useState<Batch[] | null>(null)
  const [total, setTotal] = useState(0)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let ignore = false
    // Lotes com saldo, ordenados pela validade (vencidos primeiro): uma requisição só, pequena.
    batchesApi
      .list({ productId, pageSize: BATCHES_SHOWN })
      .then((page) => {
        if (ignore) return
        setBatches(page.items)
        setTotal(page.totalItems)
      })
      .catch((err) => !ignore && setError(getErrorMessage(err)))
    return () => {
      ignore = true
    }
  }, [productId])

  const hasExpired = batches?.some((b) => b.isExpired) ?? false

  return (
    <section className="mt-3" aria-labelledby="quick-view-batches">
      <h3 id="quick-view-batches" className="h6 mb-2">
        Próximos lotes
      </h3>
      {error ? (
        <p className="small text-danger mb-0">{error}</p>
      ) : !batches ? (
        <div className="small text-secondary d-flex align-items-center gap-2" role="status">
          <Spinner animation="border" size="sm" aria-hidden="true" /> Carregando lotes...
        </div>
      ) : batches.length === 0 ? (
        <p className="small text-secondary mb-0">Nenhum lote com saldo.</p>
      ) : (
        <>
          {hasExpired && (
            <Alert variant="danger" className="py-2 small">
              Há lote vencido com saldo. <Link to="/lotes?visao=expired">Ver lotes vencidos</Link>
            </Alert>
          )}
          <ul className="list-unstyled mb-0 quick-view-batches">
            {batches.map((batch) => (
              <li key={batch.id} className="d-flex flex-wrap justify-content-between align-items-center gap-2">
                <span>
                  Lote <span className="font-monospace">{batch.batchNumber}</span>
                  <span className="text-secondary"> · {formatNumber(batch.quantityAvailable)} un.</span>
                </span>
                <ExpirationBadge expirationDate={batch.expirationDate} daysToExpire={batch.daysToExpire} />
              </li>
            ))}
          </ul>
          {total > batches.length && (
            <p className="small text-secondary mt-2 mb-0">
              e mais {total - batches.length} lote(s). <Link to="/lotes">Ver todos</Link>
            </p>
          )}
        </>
      )}
    </section>
  )
}
