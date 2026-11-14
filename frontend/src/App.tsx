import { lazy, Suspense } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router'
import { AuthProvider } from './auth/AuthProvider'
import { access } from './auth/permissions'
import { RequireAuth } from './auth/RequireAuth'
import { Loading } from './components/Loading'
import { AdminLayout } from './layouts/AdminLayout'
import { BatchesPage } from './pages/batches/BatchesPage'
import { CategoriesPage } from './pages/categories/CategoriesPage'
import { CombosPage } from './pages/combos/CombosPage'
import { CustomersPage } from './pages/customers/CustomersPage'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { ProductsPage } from './pages/products/ProductsPage'
import { PurchaseFormPage } from './pages/purchases/PurchaseFormPage'
import { PurchasesPage } from './pages/purchases/PurchasesPage'
import { PosPage } from './pages/sales/PosPage'
import { SalesPage } from './pages/sales/SalesPage'
import { StockPage } from './pages/stock/StockPage'
import { SuppliersPage } from './pages/suppliers/SuppliersPage'
import { UsersPage } from './pages/users/UsersPage'

// Telas só do ADMIN carregadas sob demanda (lazy): os relatórios usam a biblioteca de gráficos
// (Recharts), que é pesada. O código só é baixado quando o ADMIN abre a tela; os outros perfis nunca o baixam.
const ReportsPage = lazy(() => import('./pages/reports/ReportsPage').then((m) => ({ default: m.ReportsPage })))
const SettingsPage = lazy(() => import('./pages/settings/SettingsPage').then((m) => ({ default: m.SettingsPage })))
const AuditPage = lazy(() => import('./pages/audit/AuditPage').then((m) => ({ default: m.AuditPage })))

/**
 * Rotas da aplicação. O /login é público; todo o resto exige login (RequireAuth)
 * e algumas áreas exigem um perfil específico (RequireAuth roles=...).
 */
function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="login" element={<LoginPage />} />

          <Route element={<RequireAuth />}>
            <Route element={<AdminLayout />}>
              <Route index element={<HomePage />} />
              <Route path="produtos" element={<ProductsPage />} />
              <Route path="categorias" element={<CategoriesPage />} />

              <Route element={<RequireAuth roles={access.sales} />}>
                <Route path="vendas" element={<PosPage />} />
                <Route path="vendas/historico" element={<SalesPage />} />
              </Route>

              <Route element={<RequireAuth roles={access.customers} />}>
                <Route path="clientes" element={<CustomersPage />} />
              </Route>

              <Route element={<RequireAuth roles={access.stock} />}>
                <Route path="estoque" element={<StockPage />} />
                <Route path="lotes" element={<BatchesPage />} />
                <Route path="compras" element={<PurchasesPage />} />
                <Route path="compras/nova" element={<PurchaseFormPage />} />
                <Route path="fornecedores" element={<SuppliersPage />} />
              </Route>

              <Route element={<RequireAuth roles={access.admin} />}>
                <Route
                  path="relatorios"
                  element={
                    <Suspense fallback={<Loading />}>
                      <ReportsPage />
                    </Suspense>
                  }
                />
                <Route path="combos" element={<CombosPage />} />
                <Route path="usuarios" element={<UsersPage />} />
                <Route
                  path="auditoria"
                  element={
                    <Suspense fallback={<Loading />}>
                      <AuditPage />
                    </Suspense>
                  }
                />
                <Route
                  path="configuracoes"
                  element={
                    <Suspense fallback={<Loading />}>
                      <SettingsPage />
                    </Suspense>
                  }
                />
              </Route>

              <Route path="*" element={<NotFoundPage />} />
            </Route>
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}

export default App
