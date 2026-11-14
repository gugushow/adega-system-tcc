import { lazy, Suspense, useEffect, useState } from 'react'
import { Alert, Button, Modal, Table } from 'react-bootstrap'
import { getErrorMessage } from '../../api/http'
import { stockApi } from '../../api/stock'
import { Loading } from '../../components/Loading'
import type { Product } from '../../types/product'
import type { CostHistory } from '../../types/stock'
import { formatDate, formatPercent, formatUnitCost } from '../../utils/format'

// O gráfico usa o Recharts, que é pesado: só é baixado quando alguém abre o histórico de um produto.
const CostHistoryChart = lazy(() => import('../../components/charts/CostHistoryChart'))


/** Variação com cor: vermelho quando o custo subiu, verde quando caiu. */
function Change({ value }: { value: number | null }) {
  if (value === null) return <span className="text-secondary">—</span>
  const className = value > 0 ? 'text-danger' : value < 0 ? 'text-success' : 'text-secondary'
  return <span className={className}>{value > 0 ? `+${formatPercent(value)}` : formatPercent(value)}</span>
}

/** Histórico do custo de compra de um produto: gráfico da evolução e uma linha por compra. */
export function CostHistoryModal({ product, onClose }: { product: Product; onClose: () => void }) {
  const [history, setHistory] = useState<CostHistory | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let ignore = false
    stockApi
      .costHistory(product.id)
      .then((data) => !ignore && setHistory(data))
      .catch((err) => !ignore && setError(getErrorMessage(err)))
    return () => {
      ignore = true
    }
  }, [product.id])

  return (
    <Modal show onHide={onClose} size="lg" centered>
      <Modal.Header closeButton closeLabel="Fechar">
        <Modal.Title as="h5">Histórico de custo · {product.name}</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {error && <Alert variant="danger">{error}</Alert>}
        {!history && !error && <Loading />}
        {history && history.entries.length === 0 && (
          <p className="text-secondary mb-0">Este produto ainda não tem compras registradas.</p>
        )}
        {history && history.entries.length > 0 && (
          <>
            <p>
              Custo atual: <strong>{formatUnitCost(history.currentCostPrice)}</strong>
              {history.totalChangePercent !== null && (
                <>
                  {' '}
                  · Variação desde a primeira compra: <Change value={history.totalChangePercent} />
                </>
              )}
            </p>
            {/* Com uma compra só não existe evolução para desenhar. */}
            {history.entries.length > 1 && (
              <div className="mb-3" role="img" aria-label={`Gráfico da evolução do custo de ${product.name}`}>
                <Suspense fallback={<Loading />}>
                  <CostHistoryChart entries={history.entries} />
                </Suspense>
              </div>
            )}
            <Table size="sm" responsive className="mb-0">
              <thead>
                <tr>
                  <th>Data</th>
                  <th>Fornecedor</th>
                  <th>Compra</th>
                  <th className="text-end">Custo unit.</th>
                  <th className="text-end">Variação</th>
                </tr>
              </thead>
              <tbody>
                {history.entries.map((entry) => (
                  <tr key={entry.id}>
                    <td>{formatDate(entry.date)}</td>
                    <td>{entry.supplierName}</td>
                    <td>#{entry.purchaseId}</td>
                    <td className="text-end">{formatUnitCost(entry.costPrice)}</td>
                    <td className="text-end">
                      <Change value={entry.changePercent} />
                    </td>
                  </tr>
                ))}
              </tbody>
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
