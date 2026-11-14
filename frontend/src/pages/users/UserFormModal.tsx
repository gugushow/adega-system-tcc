import { useState, type FormEvent } from 'react'
import { Alert, Button, Col, Form, Modal, Row } from 'react-bootstrap'
import { getErrorMessage, getFieldErrors } from '../../api/http'
import { usersApi } from '../../api/users'
import { FieldLabel, RequiredMark } from '../../components/RequiredMark'
import { roleLabels, type User, type UserRole } from '../../types/user'

interface UserFormModalProps {
  /** null = novo usuário; preenchido = edição. */
  user: User | null
  onClose: () => void
  onSaved: (message: string) => void
}

interface FormState {
  name: string
  email: string
  password: string
  role: UserRole
  active: boolean
}

const roles = Object.keys(roleLabels) as UserRole[]

const roleHelp: Record<UserRole, string> = {
  ADMIN: 'Acesso a tudo, inclusive usuários e cancelamento de vendas.',
  VENDEDOR: 'PDV, histórico de vendas e clientes. Consulta produtos.',
  ESTOQUISTA: 'Produtos, categorias, estoque, lotes, compras e fornecedores.',
}

export function UserFormModal({ user, onClose, onSaved }: UserFormModalProps) {
  const [form, setForm] = useState<FormState>({
    name: user?.name ?? '',
    email: user?.email ?? '',
    password: '',
    role: user?.role ?? 'VENDEDOR',
    active: user?.active ?? true,
  })
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  const set = <K extends keyof FormState>(field: K, value: FormState[K]) => setForm((f) => ({ ...f, [field]: value }))

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})

    try {
      if (user) {
        await usersApi.update(user.id, {
          name: form.name,
          email: form.email,
          role: form.role,
          active: form.active,
          newPassword: form.password || null,
        })
        onSaved('Usuário atualizado com sucesso.')
      } else {
        await usersApi.create({ name: form.name, email: form.email, password: form.password, role: form.role })
        onSaved('Usuário cadastrado com sucesso.')
      }
    } catch (err) {
      setError(getErrorMessage(err))
      // Na edição o campo de senha se chama newPassword no backend; aqui é o mesmo campo da tela.
      const errors = getFieldErrors(err)
      if (errors.newPassword) errors.password = errors.newPassword
      setFieldErrors(errors)
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal show onHide={saving ? undefined : onClose} backdrop="static" keyboard={false} centered>
      <Form onSubmit={handleSubmit} noValidate>
        <Modal.Header closeButton closeLabel="Fechar">
          <Modal.Title as="h5">{user ? 'Editar usuário' : 'Novo usuário'}</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger" className="py-2">{error}</Alert>}
          <Row className="g-3">
            <Col xs={12}>
              <Form.Group controlId="user-name">
                <Form.Label>Nome<RequiredMark /></Form.Label>
                <Form.Control
                  value={form.name}
                  maxLength={100}
                  autoFocus
                  onChange={(e) => set('name', e.target.value)}
                  isInvalid={!!fieldErrors.name}
                />
                <Form.Control.Feedback type="invalid">{fieldErrors.name}</Form.Control.Feedback>
              </Form.Group>
            </Col>
            <Col xs={12}>
              <Form.Group controlId="user-email">
                <Form.Label>E-mail (login)<RequiredMark /></Form.Label>
                <Form.Control
                  type="email"
                  value={form.email}
                  maxLength={150}
                  autoComplete="off"
                  onChange={(e) => set('email', e.target.value)}
                  isInvalid={!!fieldErrors.email}
                />
                <Form.Control.Feedback type="invalid">{fieldErrors.email}</Form.Control.Feedback>
              </Form.Group>
            </Col>
            <Col xs={12}>
              <Form.Group controlId="user-password">
                <Form.Label>
                  <FieldLabel text={user ? 'Nova senha' : 'Senha *'} />
                </Form.Label>
                <Form.Control
                  type="password"
                  value={form.password}
                  maxLength={100}
                  autoComplete="new-password"
                  onChange={(e) => set('password', e.target.value)}
                  isInvalid={!!fieldErrors.password}
                />
                <Form.Control.Feedback type="invalid">{fieldErrors.password}</Form.Control.Feedback>
                <Form.Text>
                  {user ? 'Deixe em branco para manter a senha atual. ' : ''}Pelo menos 8 caracteres, com letras e
                  números.
                </Form.Text>
              </Form.Group>
            </Col>
            <Col xs={12}>
              <Form.Group controlId="user-role">
                <Form.Label>Perfil<RequiredMark /></Form.Label>
                <Form.Select
                  value={form.role}
                  onChange={(e) => set('role', e.target.value as UserRole)}
                  isInvalid={!!fieldErrors.role}
                >
                  {roles.map((role) => (
                    <option key={role} value={role}>
                      {roleLabels[role]}
                    </option>
                  ))}
                </Form.Select>
                <Form.Control.Feedback type="invalid">{fieldErrors.role}</Form.Control.Feedback>
                <Form.Text>{roleHelp[form.role]}</Form.Text>
              </Form.Group>
            </Col>
            {user && (
              <Col xs={12}>
                <Form.Check
                  type="switch"
                  id="user-active"
                  label="Ativo (pode entrar no sistema)"
                  checked={form.active}
                  onChange={(e) => set('active', e.target.checked)}
                />
              </Col>
            )}
          </Row>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="outline-secondary" onClick={onClose} disabled={saving}>
            Cancelar
          </Button>
          <Button type="submit" disabled={saving}>
            {saving ? 'Salvando...' : 'Salvar'}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  )
}
