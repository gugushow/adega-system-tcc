import { access } from '../auth/permissions'
import type { UserRole } from '../types/user'

export interface MenuItem {
  label: string
  to: string
  /** Classe do Bootstrap Icons (https://icons.getbootstrap.com). */
  icon: string
  /** Perfis que veem o item. O mesmo controle protege a rota em App.tsx. */
  roles: UserRole[]
}

export interface MenuGroup {
  /** Sem título: o grupo aparece no topo, sem cabeçalho (é o caso do Dashboard/Início). */
  title?: string
  items: MenuItem[]
}

/**
 * Menu lateral, agrupado por área de trabalho. Um grupo sem nenhum item visível para o perfil
 * logado some inteiro (o vendedor, por exemplo, não vê o grupo "Estoque").
 */
export const menuGroups: MenuGroup[] = [
  {
    items: [{ label: 'Dashboard', to: '/', icon: 'bi-speedometer2', roles: access.all }],
  },
  {
    title: 'Vendas',
    items: [
      { label: 'PDV', to: '/vendas', icon: 'bi-cart3', roles: access.sales },
      { label: 'Histórico de vendas', to: '/vendas/historico', icon: 'bi-receipt', roles: access.sales },
      { label: 'Clientes', to: '/clientes', icon: 'bi-people', roles: access.customers },
      { label: 'Combos', to: '/combos', icon: 'bi-gift', roles: access.admin },
    ],
  },
  {
    title: 'Estoque',
    items: [
      { label: 'Estoque', to: '/estoque', icon: 'bi-arrow-left-right', roles: access.stock },
      { label: 'Lotes e validade', to: '/lotes', icon: 'bi-calendar2-week', roles: access.stock },
      { label: 'Compras', to: '/compras', icon: 'bi-bag', roles: access.stock },
      { label: 'Fornecedores', to: '/fornecedores', icon: 'bi-truck', roles: access.stock },
    ],
  },
  {
    title: 'Cadastros',
    items: [
      { label: 'Produtos', to: '/produtos', icon: 'bi-box-seam', roles: access.all },
      { label: 'Categorias', to: '/categorias', icon: 'bi-tags', roles: access.all },
    ],
  },
  {
    title: 'Gestão',
    items: [
      { label: 'Relatórios', to: '/relatorios', icon: 'bi-bar-chart-line', roles: access.admin },
      { label: 'Usuários', to: '/usuarios', icon: 'bi-person-gear', roles: access.admin },
      { label: 'Auditoria', to: '/auditoria', icon: 'bi-shield-check', roles: access.admin },
      { label: 'Configurações', to: '/configuracoes', icon: 'bi-gear', roles: access.admin },
    ],
  },
]

/** Rotas que não são itens do menu: o título e o grupo para a barra superior e a aba do navegador. */
const extraPages: Record<string, { title: string; group: string }> = {
  '/compras/nova': { title: 'Nova compra', group: 'Estoque' },
}

/**
 * Título e grupo da página a partir do endereço. O "/" é "Dashboard" para o ADMIN e "Início" para os
 * outros perfis (só o ADMIN vê o dashboard). Rota desconhecida é "Página não encontrada".
 */
export function pageInfo(pathname: string, isAdmin: boolean): { title: string; group?: string } {
  const path = pathname.length > 1 ? pathname.replace(/\/$/, '') : pathname
  if (path === '/') return { title: isAdmin ? 'Dashboard' : 'Início' }
  if (extraPages[path]) return extraPages[path]
  for (const group of menuGroups) {
    const item = group.items.find((i) => i.to === path)
    // "Estoque › Estoque" não ajuda: quando o item tem o nome do grupo, mostra só o nome.
    if (item) return { title: item.label, group: group.title === item.label ? undefined : group.title }
  }
  return { title: 'Página não encontrada' }
}
