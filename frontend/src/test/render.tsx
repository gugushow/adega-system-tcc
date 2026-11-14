import { render } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { AuthContext, type AuthContextValue } from '../auth/authContext'
import type { Product } from '../types/product'
import type { UserRole } from '../types/user'

/** Mostra o endereço atual: os testes conferem para onde a tela navegou. */
function LocationProbe() {
  const location = useLocation()
  return <div data-testid="location">{location.pathname + location.search}</div>
}

/** Renderiza uma tela com usuário logado (perfil escolhido) e rotas em memória. */
export function renderWithAuth(ui: ReactNode, { role = 'ADMIN', route = '/' }: { role?: UserRole; route?: string } = {}) {
  const auth: AuthContextValue = {
    user: { id: 1, name: 'Teste', email: 'teste@adega.com', role, active: true, createdAt: '2026-10-01T00:00:00Z' },
    login: async () => auth.user!,
    logout: () => {},
    hasRole: (allowed) => allowed.includes(role),
  }
  return render(
    <AuthContext.Provider value={auth}>
      <MemoryRouter initialEntries={[route]}>
        <Routes>
          <Route path="*" element={ui} />
        </Routes>
        <LocationProbe />
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

export function makeProduct(overrides: Partial<Product> = {}): Product {
  return {
    id: 1,
    name: 'Heineken 350ml',
    description: null,
    barcode: '7891234567895',
    brand: 'Heineken',
    categoryId: 1,
    categoryName: 'Cerveja',
    volume: '350ml',
    alcoholContent: 5,
    costPrice: 3.9,
    salePrice: 6.5,
    marginPercent: 40,
    minimumStock: 5,
    currentStock: 10,
    isLowStock: false,
    isOutOfStock: false,
    active: true,
    createdAt: '2026-10-01T00:00:00Z',
    updatedAt: null,
    ...overrides,
  }
}
