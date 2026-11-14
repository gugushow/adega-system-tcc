import { useState, type FormEvent } from 'react'
import { Alert, Button, Col, Form, Modal, Row } from 'react-bootstrap'
import { customersApi } from '../../api/customers'
import { getErrorMessage, getFieldErrors } from '../../api/http'
import { access } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { FieldLabel } from '../../components/RequiredMark'
import type { Customer, CustomerInput } from '../../types/customer'
import { emptyToNull, formatCpf } from '../../utils/format'

interface CustomerFormModalProps {
  /** null = novo cliente; preenchido = edição. */
  customer: Customer | null
  onClose: () => void
  /** Recebe o cliente salvo: o PDV usa para já selecionar o cliente recém-cadastrado. */
  onSaved: (message: string, customer: Customer) => void
}

interface FormState {
  name: string
  cpf: string
  phone: string
  email: string
  birthDate: string
  active: boolean
}

function toFormState(customer: Customer | null): FormState {
  return {
    name: customer?.name ?? '',
    cpf: customer?.cpf ? formatCpf(customer.cpf) : '',
    phone: customer?.phone ?? '',
    email: customer?.email ?? '',
    birthDate: customer?.birthDate ?? '',
    active: customer?.active ?? true,
  }
}

export function CustomerFormModal({ customer, onClose, onSaved }: CustomerFormModalProps) {
  const isAdmin = useAuth().hasRole(access.admin)
  const [form, setForm] = useState<FormState>(() => toFormState(customer))
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  const set = (field: keyof FormState) => (value: string | boolean) => setForm((f) => ({ ...f, [field]: value }))

  async function handleSubmit(event: FormEvent) {
    // stopPropagation: no PDV este modal abre por cima de outra tela, e o submit não pode "vazar".
    event.preventDefault()
    event.stopPropagation()
    setSaving(true)
    setError(null)
    setFieldErrors({})

    // O CPF vai como foi digitado (com ou sem máscara): o backend valida e normaliza.
    const input: CustomerInput = {
      name: form.name,
      cpf: emptyToNull(form.cpf),
      phone: emptyToNull(form.phone),
      email: emptyToNull(form.email),
      birthDate: form.birthDate || null,
    }

    try {
      if (customer) {
        const saved = await customersApi.update(customer.id, { ...input, active: form.active })
        onSaved('Cliente atualizado com sucesso.', saved)
      } else {
        const saved = await customersApi.create(input)
        onSaved('Cliente cadastrado com sucesso.', saved)
      }
    } catch (err) {
      setError(getErrorMessage(err))
      setFieldErrors(getFieldErrors(err))
    } finally {
      setSaving(false)
    }
  }

  const field = (name: keyof FormState, label: string, props: Record<string, unknown> = {}) => (
    <Form.Group controlId={`customer-${name}`}>
      <Form.Label>
        <FieldLabel text={label} />
      </Form.Label>
      <Form.Control
        value={form[name] as string}
        onChange={(e) => set(name)(e.target.value)}
        isInvalid={!!fieldErrors[name]}
        {...props}
      />
      <Form.Control.Feedback type="invalid">{fieldErrors[name]}</Form.Control.Feedback>
    </Form.Group>
  )

  return (
    <Modal show onHide={saving ? undefined : onClose} backdrop="static" keyboard={false} size="lg" centered>
      <Form onSubmit={handleSubmit} noValidate>
        <Modal.Header closeButton closeLabel="Fechar">
          <Modal.Title as="h5">{customer ? 'Editar cliente' : 'Novo cliente'}</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger" className="py-2">{error}</Alert>}
          <Row className="g-3">
            <Col md={8}>{field('name', 'Nome *', { maxLength: 150, autoFocus: true })}</Col>
            <Col md={4}>{field('cpf', 'CPF', { maxLength: 14, placeholder: '000.000.000-00' })}</Col>
            <Col md={4}>{field('phone', 'Telefone', { maxLength: 20 })}</Col>
            <Col md={4}>{field('email', 'E-mail', { type: 'email', maxLength: 150 })}</Col>
            <Col md={4}>{field('birthDate', 'Nascimento', { type: 'date' })}</Col>
            {/* Desativar cliente é só do ADMIN (a API também ignora a mudança vinda do vendedor). */}
            {customer && isAdmin && (
              <Col xs={12}>
                <Form.Check
                  type="switch"
                  id="customer-active"
                  label="Ativo"
                  checked={form.active}
                  onChange={(e) => set('active')(e.target.checked)}
                />
              </Col>
            )}
          </Row>
          {!customer && <Form.Text>Só o nome é obrigatório. Os outros dados podem ser completados depois.</Form.Text>}
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
