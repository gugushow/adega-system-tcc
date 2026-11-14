import { useState, type FormEvent } from 'react'
import { Alert, Button, Col, Form, Modal, Row } from 'react-bootstrap'
import { getErrorMessage, getFieldErrors } from '../../api/http'
import { suppliersApi } from '../../api/suppliers'
import { FieldLabel } from '../../components/RequiredMark'
import type { Supplier, SupplierInput } from '../../types/supplier'
import { emptyToNull, formatCnpj } from '../../utils/format'

interface SupplierFormModalProps {
  /** null = novo fornecedor; preenchido = edição. */
  supplier: Supplier | null
  onClose: () => void
  onSaved: (message: string) => void
}

interface FormState {
  name: string
  corporateName: string
  cnpj: string
  phone: string
  email: string
  address: string
  active: boolean
}

function toFormState(supplier: Supplier | null): FormState {
  return {
    name: supplier?.name ?? '',
    corporateName: supplier?.corporateName ?? '',
    cnpj: supplier?.cnpj ? formatCnpj(supplier.cnpj) : '',
    phone: supplier?.phone ?? '',
    email: supplier?.email ?? '',
    address: supplier?.address ?? '',
    active: supplier?.active ?? true,
  }
}

export function SupplierFormModal({ supplier, onClose, onSaved }: SupplierFormModalProps) {
  const [form, setForm] = useState<FormState>(() => toFormState(supplier))
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  const set = (field: keyof FormState) => (value: string | boolean) => setForm((f) => ({ ...f, [field]: value }))

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})

    // O CNPJ vai como foi digitado (com ou sem máscara): o backend valida e normaliza.
    const input: SupplierInput = {
      name: form.name,
      corporateName: emptyToNull(form.corporateName),
      cnpj: emptyToNull(form.cnpj),
      phone: emptyToNull(form.phone),
      email: emptyToNull(form.email),
      address: emptyToNull(form.address),
    }

    try {
      if (supplier) {
        await suppliersApi.update(supplier.id, { ...input, active: form.active })
        onSaved('Fornecedor atualizado com sucesso.')
      } else {
        await suppliersApi.create(input)
        onSaved('Fornecedor cadastrado com sucesso.')
      }
    } catch (err) {
      setError(getErrorMessage(err))
      setFieldErrors(getFieldErrors(err))
    } finally {
      setSaving(false)
    }
  }

  const field = (name: keyof FormState, label: string, props: Record<string, unknown> = {}) => (
    <Form.Group controlId={`supplier-${name}`}>
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
          <Modal.Title as="h5">{supplier ? 'Editar fornecedor' : 'Novo fornecedor'}</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger" className="py-2">{error}</Alert>}
          <Row className="g-3">
            <Col md={6}>{field('name', 'Nome fantasia *', { maxLength: 150, autoFocus: true })}</Col>
            <Col md={6}>{field('corporateName', 'Razão social', { maxLength: 200 })}</Col>
            <Col md={4}>{field('cnpj', 'CNPJ', { maxLength: 18, placeholder: '00.000.000/0000-00' })}</Col>
            <Col md={4}>{field('phone', 'Telefone', { maxLength: 20 })}</Col>
            <Col md={4}>{field('email', 'E-mail', { type: 'email', maxLength: 150 })}</Col>
            <Col xs={12}>{field('address', 'Endereço', { maxLength: 300 })}</Col>
            {supplier && (
              <Col xs={12}>
                <Form.Check
                  type="switch"
                  id="supplier-active"
                  label="Ativo"
                  checked={form.active}
                  onChange={(e) => set('active')(e.target.checked)}
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
