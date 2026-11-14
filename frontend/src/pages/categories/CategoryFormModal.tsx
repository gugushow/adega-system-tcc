import { useState, type FormEvent } from 'react'
import { Alert, Button, Form, Modal } from 'react-bootstrap'
import { categoriesApi } from '../../api/categories'
import { getErrorMessage, getFieldErrors } from '../../api/http'
import { RequiredMark } from '../../components/RequiredMark'
import type { Category } from '../../types/category'
import { emptyToNull } from '../../utils/format'

interface CategoryFormModalProps {
  /** null = nova categoria; preenchido = edição. */
  category: Category | null
  onClose: () => void
  onSaved: (message: string) => void
}

/**
 * Formulário de cadastro e edição. A página monta este componente só quando o modal abre,
 * então o estado inicial (useState) já nasce com os dados da categoria escolhida.
 */
export function CategoryFormModal({ category, onClose, onSaved }: CategoryFormModalProps) {
  const [name, setName] = useState(category?.name ?? '')
  const [description, setDescription] = useState(category?.description ?? '')
  const [active, setActive] = useState(category?.active ?? true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})
    try {
      const input = { name, description: emptyToNull(description) }
      if (category) {
        await categoriesApi.update(category.id, { ...input, active })
        onSaved('Categoria atualizada com sucesso.')
      } else {
        await categoriesApi.create(input)
        onSaved('Categoria cadastrada com sucesso.')
      }
    } catch (err) {
      // A validação final é sempre do backend; aqui só mostramos o que ele respondeu.
      setError(getErrorMessage(err))
      setFieldErrors(getFieldErrors(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal show onHide={saving ? undefined : onClose} backdrop="static" keyboard={false} centered>
      <Form onSubmit={handleSubmit} noValidate>
        <Modal.Header closeButton closeLabel="Fechar">
          <Modal.Title as="h5">{category ? 'Editar categoria' : 'Nova categoria'}</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger" className="py-2">{error}</Alert>}
          <Form.Group className="mb-3" controlId="category-name">
            <Form.Label>Nome<RequiredMark /></Form.Label>
            <Form.Control
              value={name}
              onChange={(e) => setName(e.target.value)}
              isInvalid={!!fieldErrors.name}
              maxLength={100}
              autoFocus
            />
            <Form.Control.Feedback type="invalid">{fieldErrors.name}</Form.Control.Feedback>
          </Form.Group>
          <Form.Group className="mb-3" controlId="category-description">
            <Form.Label>Descrição</Form.Label>
            <Form.Control
              as="textarea"
              rows={2}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              isInvalid={!!fieldErrors.description}
              maxLength={500}
            />
            <Form.Control.Feedback type="invalid">{fieldErrors.description}</Form.Control.Feedback>
          </Form.Group>
          {category && (
            <Form.Check
              type="switch"
              id="category-active"
              label="Ativa"
              checked={active}
              onChange={(e) => setActive(e.target.checked)}
            />
          )}
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
