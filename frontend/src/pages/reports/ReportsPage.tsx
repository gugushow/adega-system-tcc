import { Tab, Tabs } from 'react-bootstrap'
import { Link } from 'react-router'
import { CategoriesReportTab } from './CategoriesReportTab'
import { IdleProductsReportTab } from './IdleProductsReportTab'
import { MarginsReportTab } from './MarginsReportTab'
import { ProductsReportTab } from './ProductsReportTab'
import { PurchasesReportTab } from './PurchasesReportTab'
import { SalesReportTab } from './SalesReportTab'
import { StockReportTab } from './StockReportTab'

/**
 * Relatórios gerenciais (só ADMIN). Cada aba carrega os próprios dados só quando é aberta
 * (mountOnEnter), para não fazer 7 consultas de uma vez.
 */
export function ReportsPage() {
  return (
    <>
      <h1 className="h4 mb-1">Relatórios</h1>
      <p className="text-secondary small mb-3">
        Vendas canceladas nunca entram nos números. Para estoque baixo e zerado veja{' '}
        <Link to="/produtos">Produtos</Link>, para validades veja <Link to="/lotes">Lotes</Link> e para o histórico de
        entradas e saídas veja <Link to="/estoque">Estoque</Link>.
      </p>
      <Tabs defaultActiveKey="sales" mountOnEnter className="mb-3">
        <Tab eventKey="sales" title="Vendas">
          <SalesReportTab />
        </Tab>
        <Tab eventKey="products" title="Produtos">
          <ProductsReportTab />
        </Tab>
        <Tab eventKey="categories" title="Categorias">
          <CategoriesReportTab />
        </Tab>
        <Tab eventKey="idle" title="Parados">
          <IdleProductsReportTab />
        </Tab>
        <Tab eventKey="margins" title="Margens">
          <MarginsReportTab />
        </Tab>
        <Tab eventKey="purchases" title="Compras">
          <PurchasesReportTab />
        </Tab>
        <Tab eventKey="stock" title="Estoque">
          <StockReportTab />
        </Tab>
      </Tabs>
    </>
  )
}
