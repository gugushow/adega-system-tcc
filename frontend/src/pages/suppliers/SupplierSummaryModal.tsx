import { useEffect, useState } from 'react'
import { Alert, Button, Col, Modal, Row, Table } from 'react-bootstrap'
import { getErrorMessage } from '../../api/http'
import { suppliersApi } from '../../api/suppliers'
import { Loading } from '../../components/Loading'
import type { Purchase } from '../../types/purchase'
import type { Supplier, SupplierSummary } from '../../types/supplier'
import { formatCurrency, formatDate } from '../../utils/format'

interface SupplierSummaryModalProps {
  supplier: Supplier
  onClose: () => void
}

/** Resumo do fornecedor: totais, produtos mais comprados e as últimas compras. */
export function SupplierSummaryModal({ supplier, onClose }: SupplierSummaryModalProps) {
  const [summary, setSummary] = useState<SupplierSummary | null>(null)
  const [purchases, setPurchases] = useState<Purchase[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let ignore = false
    Promise.all([suppliersApi.summary(supplier.id), suppliersApi.purchases(supplier.id, { pageSize: 5 })])
      .then(([summaryData, purchasesData]) => {
        if (ignore) return
        setSummary(summaryData)
        setPurchases(purchasesData.items)
      })
      .catch((err) => !ignore && setError(getErrorMessage(err)))
    return () => {
      ignore = true
    }
  }, [supplier.id])

  return (
    <Modal show onHide={onClose} size="lg" centered>
      <Modal.Header closeButton closeLabel="Fechar">
        <Modal.Title as="h5">{supplier.name}</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {error && <Alert variant="danger">{error}</Alert>}
        {!summary && !error && <Loading />}
        {summary && (
          <>
            <Row className="g-3 mb-4 text-center">
              <Col sm={4}>
                <div className="text-secondary small">Compras</div>
                <div className="fs-4 fw-semibold">{summary.purchaseCount}</div>
              </Col>
              <Col sm={4}>
                <div className="text-secondary small">Total comprado</div>
                <div className="fs-4 fw-semibold">{formatCurrency(summary.totalPurchased)}</div>
              </Col>
              <Col sm={4}>
                <div className="text-secondary small">Última compra</div>
                <div className="fs-4 fw-semibold">
                  {summary.lastPurchaseDate ? formatDate(summary.lastPurchaseDate) : '—'}
                </div>
              </Col>
            </Row>

            <h6>Produtos mais comprados</h6>
            {summary.topProducts.length === 0 ? (
              <p className="text-secondary">Nenhuma compra registrada para este fornecedor.</p>
            ) : (
              <Table size="sm" responsive className="mb-4">
                <thead>
                  <tr>
                    <th>Produto</th>
                    <th className="text-end">Unidades</th>
                    <th className="text-end">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.topProducts.map((p) => (
                    <tr key={p.productId}>
                      <td>{p.productName}</td>
                      <td className="text-end">{p.totalQuantity}</td>
                      <td className="text-end">{formatCurrency(p.totalAmount)}</td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}

            {purchases.length > 0 && (
              <>
                <h6>Últimas compras</h6>
                <Table size="sm" responsive className="mb-0">
                  <thead>
                    <tr>
                      <th>Data</th>
                      <th>Nota</th>
                      <th className="text-end">Itens</th>
                      <th className="text-end">Total</th>
                    </tr>
                  </thead>
                  <tbody>
                    {purchases.map((p) => (
                      <tr key={p.id}>
                        <td>{formatDate(p.purchaseDate)}</td>
                        <td>{p.invoiceNumber ?? '—'}</td>
                        <td className="text-end">{p.itemCount}</td>
                        <td className="text-end">{formatCurrency(p.totalAmount)}</td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              </>
            )}
          </>
        )}
      </Modal.Body>
      <Modal.Footer>
        <Button variant="outline-secondary" onClick={onClose}>
          Fechar
        </Button>
      </Modal.Footer>
    </Modal>
  )
}
