import { lazy, Suspense } from 'react'
import { Card, Col, Row } from 'react-bootstrap'
import { Link } from 'react-router'
import { access } from '../auth/permissions'
import { useAuth } from '../auth/useAuth'
import { roleLabels } from '../types/user'
import { QuickProductLookup } from '../components/barcode/QuickProductLookup'
import { Loading } from '../components/Loading'

// O dashboard tem gráficos (Recharts, pesado): só é baixado quando um ADMIN entra.
const DashboardPage = lazy(() => import('./dashboard/DashboardPage').then((m) => ({ default: m.DashboardPage })))

interface Shortcut {
  label: string
  description: string
  to: string
}

const shortcuts: Record<'VENDEDOR' | 'ESTOQUISTA', Shortcut[]> = {
  VENDEDOR: [
    { label: 'PDV', description: 'Registrar uma venda', to: '/vendas' },
    { label: 'Histórico de vendas', description: 'Consultar vendas feitas', to: '/vendas/historico' },
    { label: 'Clientes', description: 'Cadastrar e consultar clientes', to: '/clientes' },
  ],
  ESTOQUISTA: [
    { label: 'Compras', description: 'Registrar entrada de mercadoria', to: '/compras/nova' },
    { label: 'Lotes', description: 'Validades e baixa de vencidos', to: '/lotes' },
    { label: 'Estoque', description: 'Movimentações e ajustes', to: '/estoque' },
  ],
}

/**
 * Página inicial. O ADMIN vê o dashboard (faturamento e lucro são informação de gestão);
 * os outros perfis veem atalhos para o que usam no dia a dia.
 */
export function HomePage() {
  const { user, hasRole } = useAuth()

  if (hasRole(access.admin)) {
    return (
      <Suspense fallback={<Loading />}>
        <DashboardPage />
      </Suspense>
    )
  }

  const items = user && user.role !== 'ADMIN' ? shortcuts[user.role] : []

  return (
    <>
      <div className="d-flex flex-wrap justify-content-between align-items-start gap-3 mb-4">
        <div>
          <h1 className="h4 mb-1">Olá, {user?.name}</h1>
          <p className="text-secondary mb-0">Perfil: {user ? roleLabels[user.role] : ''}</p>
        </div>
        <QuickProductLookup />
      </div>
      <Row xs={1} md={3} className="g-3">
        {items.map((item) => (
          <Col key={item.to}>
            <Card as={Link} to={item.to} className="h-100 text-decoration-none">
              <Card.Body>
                <Card.Title as="h2" className="h6">{item.label}</Card.Title>
                <Card.Text className="text-secondary small">{item.description}</Card.Text>
              </Card.Body>
            </Card>
          </Col>
        ))}
      </Row>
    </>
  )
}
