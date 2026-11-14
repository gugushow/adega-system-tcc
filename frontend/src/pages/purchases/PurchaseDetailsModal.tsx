import { useEffect, useState } from 'react'
import { Alert, Button, Modal, Table } from 'react-bootstrap'
import { getErrorMessage } from '../../api/http'
import { purchasesApi } from '../../api/purchases'
import { Loading } from '../../components/Loading'
import type { PurchaseDetails } from '../../types/purchase'
import { formatCurrency, formatDate, formatDateTime, formatUnitCost } from '../../utils/format'


export function PurchaseDetailsModal({ purchaseId, onClose }: { purchaseId: number; onClose: () => void }) {
  const [purchase, setPurchase] = useState<PurchaseDetails | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let ignore = false
    purchasesApi
      .get(purchaseId)
      .then((data) => !ignore && setPurchase(data))
      .catch((err) => !ignore && setError(getErrorMessage(err)))
    return () => {
      ignore = true
    }
  }, [purchaseId])

  return (
    <Modal show onHide={onClose} size="lg" centered>
      <Modal.Header closeButton closeLabel="Fechar">
        <Modal.Title as="h5">Compra #{purchaseId}</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {error && <Alert variant="danger">{error}</Alert>}
        {!purchase && !error && <Loading />}
        {purchase && (
          <>
            <dl className="row mb-3">
              <dt className="col-sm-3">Fornecedor</dt>
              <dd className="col-sm-9">{purchase.supplierName}</dd>
              <dt className="col-sm-3">Nota fiscal</dt>
              <dd className="col-sm-9">{purchase.invoiceNumber ?? '—'}</dd>
              <dt className="col-sm-3">Data da compra</dt>
              <dd className="col-sm-9">{formatDate(purchase.purchaseDate)}</dd>
              <dt className="col-sm-3">Registrada por</dt>
              <dd className="col-sm-9">
                {purchase.userName} em {formatDateTime(purchase.createdAt)}
              </dd>
            </dl>
            <Table size="sm" responsive>
              <thead>
                <tr>
                  <th>Produto</th>
                  <th>Lote</th>
                  <th>Validade</th>
                  <th className="text-end">Qtd.</th>
                  <th className="text-end">Custo unit.</th>
                  <th className="text-end">Total</th>
                </tr>
              </thead>
              <tbody>
                {purchase.items.map((item) => (
                  <tr key={item.id}>
                    <td>{item.productName}</td>
                    <td className="font-monospace small">{item.batchNumber ?? '—'}</td>
                    <td>{item.expirationDate ? formatDate(item.expirationDate) : '—'}</td>
                    <td className="text-end">{item.quantity}</td>
                    <td className="text-end">{formatUnitCost(item.unitCost)}</td>
                    <td className="text-end">{formatCurrency(item.total)}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <th colSpan={5} className="text-end">
                    Total da compra
                  </th>
                  <th className="text-end">{formatCurrency(purchase.totalAmount)}</th>
                </tr>
              </tfoot>
            </Table>
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
