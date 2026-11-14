import { useState } from 'react'
import { Alert, Badge, Card, Form, Table } from 'react-bootstrap'
import { Link } from 'react-router'
import { reportsApi } from '../../api/reports'
import { EmptyState } from '../../components/EmptyState'
import { useReport } from '../../hooks/useReport'
import { formatCurrency, formatPercent } from '../../utils/format'
import { ReportState } from './ReportState'

/** Margem de cada produto ativo (preço de venda × custo), destacando quem está abaixo do mínimo. */
export function MarginsReportTab() {
  const [onlyBelowMinimum, setOnlyBelowMinimum] = useState(false)
  const report = useReport(() => reportsApi.margins(onlyBelowMinimum), [onlyBelowMinimum])

  return (
    <>
      <Form.Check
        type="switch"
        id="only-below-minimum"
        className="mb-3"
        label="Mostrar só os produtos abaixo da margem mínima"
        checked={onlyBelowMinimum}
        onChange={(e) => setOnlyBelowMinimum(e.target.checked)}
      />

      <ReportState {...report}>
        {(data) => (
          <>
            <p className="text-secondary">
              Margem mínima configurada: <strong>{formatPercent(data.minimumMarginPercent)}</strong> (
              <Link to="/configuracoes">alterar</Link>). Margem = (venda − custo) ÷ venda.
            </p>
            {data.belowMinimumCount > 0 && (
              <Alert variant="warning">
                {data.belowMinimumCount === 1
                  ? '1 produto está com margem abaixo do mínimo configurado.'
                  : `${data.belowMinimumCount} produtos estão com margem abaixo do mínimo configurado.`}
              </Alert>
            )}
            {data.items.length === 0 ? (
              <EmptyState title={onlyBelowMinimum ? 'Nenhum produto abaixo da margem mínima' : 'Nenhum produto ativo'} />
            ) : (
              <Card>
                <Table responsive hover size="sm" className="mb-0 align-middle">
                  <thead>
                    <tr>
                      <th className="ps-3">Produto</th>
                      <th>Categoria</th>
                      <th className="text-end">Custo</th>
                      <th className="text-end">Venda</th>
                      <th className="text-end pe-3">Margem</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.items.map((p) => (
                      <tr key={p.productId}>
                        <td className="ps-3">{p.productName}</td>
                        <td>{p.categoryName}</td>
                        <td className="text-end">{formatCurrency(p.costPrice)}</td>
                        <td className="text-end">{formatCurrency(p.salePrice)}</td>
                        <td className="text-end pe-3">
                          {p.belowMinimum && (
                            <Badge bg="danger" className="me-2">Abaixo do mínimo</Badge>
                          )}
                          <span className={p.belowMinimum ? 'text-danger fw-semibold' : undefined}>
                            {formatPercent(p.marginPercent)}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              </Card>
            )}
          </>
        )}
      </ReportState>
    </>
  )
}
