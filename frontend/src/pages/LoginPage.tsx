import { useState, type FormEvent } from 'react'
import { Alert, Button, Card, Form } from 'react-bootstrap'
import { Navigate, useLocation, useNavigate, useSearchParams } from 'react-router'
import { getErrorMessage, getFieldErrors } from '../api/http'
import { useAuth } from '../auth/useAuth'
import { BrandMark } from '../components/BrandMark'
import { usePageTitle } from '../hooks/usePageTitle'
import { ThemeToggle } from '../theme/ThemeToggle'
import type { User } from '../types/user'
import './LoginPage.css'

/** Página inicial de cada perfil depois do login: o vendedor cai direto no PDV. */
function homeFor(user: User) {
  return user.role === 'VENDEDOR' ? '/vendas' : '/'
}

export function LoginPage() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [searchParams] = useSearchParams()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  usePageTitle('Entrar')

  // Já logado (ex.: abriu /login de novo): vai direto para o sistema.
  if (user) {
    return <Navigate to={homeFor(user)} replace />
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    setFieldErrors({})
    try {
      const logged = await login({ email, password })
      // Volta para a página que a pessoa tentou abrir antes do login, ou onde estava quando a sessão
      // expirou (?de=). Só aceita caminhos internos ("/..."), nunca um endereço de outro site.
      const from = (location.state as { from?: string } | null)?.from ?? searchParams.get('de')
      const isInternal = !!from && from.startsWith('/') && !from.startsWith('//') && !from.startsWith('/login')
      navigate(isInternal && from !== '/' ? from : homeFor(logged), { replace: true })
    } catch (err) {
      setError(getErrorMessage(err))
      setFieldErrors(getFieldErrors(err))
      setSubmitting(false)
    }
  }

  return (
    <div className="login-page d-flex align-items-center justify-content-center p-3">
      {/* O seletor de tema também fica disponível antes de entrar. */}
      <div className="login-theme">
        <ThemeToggle />
      </div>
      <Card className="login-card">
        <Card.Body className="p-4 p-sm-5">
          <div className="text-center mb-4">
            <span className="login-mark">
              <BrandMark size={28} />
            </span>
            <h1 className="h4 mb-1">Adega System</h1>
            <p className="text-secondary mb-0">Entre com o seu e-mail e senha</p>
          </div>

          {searchParams.get('sessao') === 'expirada' && !error && (
            <Alert variant="warning" className="py-2">
              Sua sessão expirou. Entre novamente.
            </Alert>
          )}
          {error && (
            <Alert variant="danger" className="py-2">
              {error}
            </Alert>
          )}

          <Form onSubmit={handleSubmit} noValidate>
            <Form.Group className="mb-3" controlId="login-email">
              <Form.Label>E-mail</Form.Label>
              <Form.Control
                type="email"
                autoComplete="username"
                autoFocus
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                isInvalid={!!fieldErrors.email}
              />
              <Form.Control.Feedback type="invalid">{fieldErrors.email}</Form.Control.Feedback>
            </Form.Group>
            <Form.Group className="mb-4" controlId="login-password">
              <Form.Label>Senha</Form.Label>
              <Form.Control
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                isInvalid={!!fieldErrors.password}
              />
              <Form.Control.Feedback type="invalid">{fieldErrors.password}</Form.Control.Feedback>
            </Form.Group>
            <Button type="submit" size="lg" className="w-100" disabled={submitting}>
              {submitting ? 'Entrando...' : 'Entrar'}
            </Button>
          </Form>
        </Card.Body>
      </Card>
    </div>
  )
}
