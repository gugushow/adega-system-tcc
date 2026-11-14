import { Link, Navigate, Outlet, useLocation } from 'react-router'
import { EmptyState } from '../components/EmptyState'
import type { UserRole } from '../types/user'
import { useAuth } from './useAuth'

interface RequireAuthProps {
  /** Perfis que podem abrir as rotas de dentro. Sem a lista, basta estar logado. */
  roles?: UserRole[]
}

/**
 * "Porteiro" das rotas: sem login, manda para /login (lembrando a página pedida);
 * logado mas sem o perfil necessário, mostra o aviso de acesso negado.
 */
export function RequireAuth({ roles }: RequireAuthProps) {
  const { user, hasRole } = useAuth()
  const location = useLocation()

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if (roles && !hasRole(roles)) {
    return (
      <EmptyState
        icon="bi-lock"
        title="Acesso negado"
        description="O seu perfil não tem permissão para abrir esta página. Se precisar dela, fale com o administrador."
        action={
          <Link to="/" className="btn btn-primary">
            Voltar ao início
          </Link>
        }
      />
    )
  }

  return <Outlet />
}
