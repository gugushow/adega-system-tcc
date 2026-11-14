import { useState, type FormEvent } from 'react'
import { Alert, Button, Form, Modal } from 'react-bootstrap'
import { authApi } from '../api/auth'
import { getErrorMessage, getFieldErrors } from '../api/http'
import { FieldLabel } from './RequiredMark'

interface ChangePasswordModalProps {
  onClose: () => void
  onChanged: (message: string) => void
}

/** O próprio usuário troca a senha, confirmando a senha atual. */
export function ChangePasswordModal({ onClose, onChanged }: ChangePasswordModalProps) {
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setFieldErrors({})

    // A confirmação só existe na tela: é para pegar erro de digitação antes de enviar.
    if (newPassword !== confirmation) {
      setFieldErrors({ confirmation: 'A confirmação não confere com a nova senha.' })
      return
    }

    setSaving(true)
    try {
      await authApi.changePassword({ currentPassword, newPassword })
      onChanged('Senha alterada com sucesso.')
    } catch (err) {
      setError(getErrorMessage(err))
      setFieldErrors(getFieldErrors(err))
    } finally {
      setSaving(false)
    }
  }

  const field = (
    id: string,
    label: string,
    value: string,
    onChange: (value: string) => void,
    autoComplete: string,
  ) => (
    <Form.Group className="mb-3" controlId={`password-${id}`}>
      <Form.Label>
        <FieldLabel text={label} />
      </Form.Label>
      <Form.Control
        type="password"
        autoComplete={autoComplete}
        autoFocus={id === 'currentPassword'}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        isInvalid={!!fieldErrors[id]}
      />
      <Form.Control.Feedback type="invalid">{fieldErrors[id]}</Form.Control.Feedback>
    </Form.Group>
  )

  return (
    <Modal show onHide={saving ? undefined : onClose} backdrop="static" keyboard={false} centered>
      <Form onSubmit={handleSubmit} noValidate>
        <Modal.Header closeButton closeLabel="Fechar">
          <Modal.Title as="h5">Alterar minha senha</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger" className="py-2">{error}</Alert>}
          {field('currentPassword', 'Senha atual *', currentPassword, setCurrentPassword, 'current-password')}
          {field('newPassword', 'Nova senha *', newPassword, setNewPassword, 'new-password')}
          {field('confirmation', 'Confirme a nova senha *', confirmation, setConfirmation, 'new-password')}
          <Form.Text>Pelo menos 8 caracteres, com letras e números.</Form.Text>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="outline-secondary" onClick={onClose} disabled={saving}>
            Cancelar
          </Button>
          <Button type="submit" disabled={saving}>
            {saving ? 'Salvando...' : 'Alterar senha'}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  )
}
