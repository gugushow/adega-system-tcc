import { useEffect, useRef, useState } from 'react'
import { Alert, Button, Dropdown, Nav, Offcanvas } from 'react-bootstrap'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router'
import { access } from '../auth/permissions'
import { useAuth } from '../auth/useAuth'
import { BrandMark } from '../components/BrandMark'
import { ChangePasswordModal } from '../components/ChangePasswordModal'
import { usePageTitle } from '../hooks/usePageTitle'
import { ThemeToggle } from '../theme/ThemeToggle'
import { roleLabels } from '../types/user'
import { menuGroups, pageInfo } from './menu'
import './AdminLayout.css'

const SIDEBAR_KEY = 'adega.sidebar'

/** Lê se o menu estava recolhido na última visita (só no desktop; no celular ele é uma gaveta). */
function loadCollapsed() {
  try {
    return localStorage.getItem(SIDEBAR_KEY) === 'collapsed'
  } catch {
    return false
  }
}

function saveCollapsed(collapsed: boolean) {
  try {
    if (collapsed) localStorage.setItem(SIDEBAR_KEY, 'collapsed')
    else localStorage.removeItem(SIDEBAR_KEY)
  } catch {
    // sem armazenamento: a escolha vale até recarregar
  }
}

/** Iniciais do nome ("Maria Souza" → "MS"), usadas no avatar. */
function initials(name: string) {
  const parts = name.trim().split(/\s+/)
  const first = parts[0]?.[0] ?? ''
  const last = parts.length > 1 ? parts[parts.length - 1][0] : ''
  return (first + last).toUpperCase()
}

function Brand() {
  return (
    <div className="sidebar-brand">
      <span className="brand-mark">
        <BrandMark />
      </span>
      <span className="sidebar-label fw-semibold">Adega System</span>
    </div>
  )
}

/** Itens do menu agrupados. Só aparece o que o perfil logado pode abrir. */
function SidebarMenu({ onNavigate }: { onNavigate?: () => void }) {
  const { hasRole } = useAuth()
  const isAdmin = hasRole(access.admin)
  const { pathname } = useLocation()
  const navRef = useRef<HTMLElement>(null)

  // Em telas baixas o menu rola: garante que o item da página atual fique à vista.
  useEffect(() => {
    navRef.current?.querySelector('.nav-link.active')?.scrollIntoView({ block: 'nearest' })
  }, [pathname])

  return (
    <nav aria-label="Menu principal" className="sidebar-nav" ref={navRef}>
      {menuGroups.map((group, index) => {
        const items = group.items.filter((item) => hasRole(item.roles))
        if (items.length === 0) return null
        return (
          <div key={group.title ?? index} className="sidebar-group">
            {group.title && <div className="sidebar-group-title">{group.title}</div>}
            <Nav className="flex-column">
              {items.map((item) => {
                // Só o ADMIN vê o dashboard; para os outros perfis a página inicial são atalhos.
                const label = item.to === '/' && !isAdmin ? 'Início' : item.label
                const icon = item.to === '/' && !isAdmin ? 'bi-house' : item.icon
                return (
                  <Nav.Link key={item.to} as={NavLink} to={item.to} end onClick={onNavigate} title={label}>
                    <i className={`bi ${icon}`} aria-hidden="true" />
                    <span className="sidebar-label">{label}</span>
                  </Nav.Link>
                )
              })}
            </Nav>
          </div>
        )
      })}
    </nav>
  )
}

/** Rodapé do menu: quem está logado, o perfil e o botão de sair. */
function SidebarUser({ onLogout }: { onLogout: () => void }) {
  const { user } = useAuth()
  if (!user) return null

  return (
    <div className="sidebar-user">
      <span className="avatar" aria-hidden="true">
        {initials(user.name)}
      </span>
      <div className="sidebar-label sidebar-user-info">
        <div className="text-truncate sidebar-user-name">{user.name}</div>
        <div className="text-truncate sidebar-user-role">{roleLabels[user.role]}</div>
      </div>
      <Button variant="link" className="sidebar-icon-button" onClick={onLogout} aria-label="Sair" title="Sair">
        <i className="bi bi-box-arrow-right" aria-hidden="true" />
      </Button>
    </div>
  )
}

/**
 * Layout das telas internas:
 * - menu lateral fixo no desktop (recolhível, só com ícones) e gaveta (Offcanvas) no celular;
 * - barra superior com o nome da página, o seletor de tema e o menu do usuário;
 * - o conteúdo de cada página é renderizado no <Outlet />.
 */
export function AdminLayout() {
  const { user, logout, hasRole } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const page = pageInfo(location.pathname, hasRole(access.admin))
  usePageTitle(page.title)
  const [showMenu, setShowMenu] = useState(false)
  const [collapsed, setCollapsed] = useState(loadCollapsed)
  const [changingPassword, setChangingPassword] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)

  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  function toggleCollapsed() {
    saveCollapsed(!collapsed)
    setCollapsed(!collapsed)
  }

  return (
    <div className={`admin-layout${collapsed ? ' sidebar-collapsed' : ''}`}>
      <aside className="admin-sidebar d-none d-lg-flex">
        <div className="sidebar-header">
          <Brand />
          <Button
            variant="link"
            className="sidebar-icon-button sidebar-collapse"
            onClick={toggleCollapsed}
            aria-label={collapsed ? 'Expandir menu' : 'Recolher menu'}
            aria-expanded={!collapsed}
            title={collapsed ? 'Expandir menu' : 'Recolher menu'}
          >
            <i className={`bi ${collapsed ? 'bi-chevron-double-right' : 'bi-chevron-double-left'}`} aria-hidden="true" />
          </Button>
        </div>
        <SidebarMenu />
        <SidebarUser onLogout={handleLogout} />
      </aside>

      <Offcanvas show={showMenu} onHide={() => setShowMenu(false)} className="admin-sidebar admin-sidebar-mobile">
        <Offcanvas.Header closeButton closeLabel="Fechar menu" closeVariant="white">
          <Offcanvas.Title as="div">
            <Brand />
          </Offcanvas.Title>
        </Offcanvas.Header>
        <Offcanvas.Body className="d-flex flex-column p-0">
          <SidebarMenu onNavigate={() => setShowMenu(false)} />
          <SidebarUser onLogout={handleLogout} />
        </Offcanvas.Body>
      </Offcanvas>

      <div className="admin-main">
        <header className="admin-topbar">
          <Button
            variant="outline-secondary"
            size="sm"
            className="topbar-button d-lg-none"
            onClick={() => setShowMenu(true)}
            aria-label="Abrir menu"
          >
            <i className="bi bi-list" aria-hidden="true" />
          </Button>

          {/* No celular a página já mostra o próprio título logo abaixo: aqui fica só a marca. */}
          <div className="topbar-brand d-md-none">
            <span className="brand-mark brand-mark-sm">
              <BrandMark size={16} />
            </span>
            <span className="fw-semibold">Adega System</span>
          </div>

          {/* Onde o usuário está (grupo › página), discreto: a página repete o nome no próprio título. */}
          <nav className="topbar-title text-truncate d-none d-md-block" aria-label="Você está em">
            {page.group && (
              <>
                <span className="text-secondary">{page.group}</span>
                <i className="bi bi-chevron-right topbar-separator" aria-hidden="true" />
              </>
            )}
            <span className="fw-semibold" aria-current="page">
              {page.title}
            </span>
          </nav>

          <div className="ms-auto d-flex align-items-center gap-2">
            <ThemeToggle />
            {user && (
              <Dropdown align="end">
                <Dropdown.Toggle
                  variant="outline-secondary"
                  size="sm"
                  className="topbar-button topbar-user"
                  aria-label={`Conta de ${user.name}`}
                >
                  <span className="avatar avatar-sm" aria-hidden="true">
                    {initials(user.name)}
                  </span>
                </Dropdown.Toggle>
                <Dropdown.Menu>
                  <Dropdown.ItemText>
                    <div className="fw-semibold">{user.name}</div>
                    <div className="small text-secondary">{user.email}</div>
                    <div className="small text-secondary">{roleLabels[user.role]}</div>
                  </Dropdown.ItemText>
                  <Dropdown.Divider />
                  <Dropdown.Item as="button" onClick={() => setChangingPassword(true)}>
                    <i className="bi bi-key me-2" aria-hidden="true" />
                    Alterar minha senha
                  </Dropdown.Item>
                  <Dropdown.Item as="button" onClick={handleLogout}>
                    <i className="bi bi-box-arrow-right me-2" aria-hidden="true" />
                    Sair
                  </Dropdown.Item>
                </Dropdown.Menu>
              </Dropdown>
            )}
          </div>
        </header>

        <main className="admin-content">
          {notice && (
            <Alert variant="success" dismissible closeLabel="Fechar" onClose={() => setNotice(null)}>
              {notice}
            </Alert>
          )}
          <Outlet />
        </main>
      </div>

      {changingPassword && (
        <ChangePasswordModal
          onClose={() => setChangingPassword(false)}
          onChanged={(message) => {
            setChangingPassword(false)
            setNotice(message)
          }}
        />
      )}
    </div>
  )
}
