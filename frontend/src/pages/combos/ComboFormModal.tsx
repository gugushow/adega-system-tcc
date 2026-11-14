import { useState, type FormEvent } from 'react'
import { Alert, Button, Col, Form, InputGroup, Modal, Row, Table } from 'react-bootstrap'
import { combosApi } from '../../api/combos'
import { getErrorMessage, getFieldErrors } from '../../api/http'
import { ProductPicker } from '../../components/ProductPicker'
import { RequiredMark } from '../../components/RequiredMark'
import type { Combo } from '../../types/combo'
import type { Product } from '../../types/product'
import { emptyToNull, formatCurrency, formatPercent, parseDecimal } from '../../utils/format'

interface ComboFormModalProps {
  /** null = novo combo; preenchido = edição. */
  combo: Combo | null
  onClose: () => void
  onSaved: (message: string) => void
}

/** Uma linha da tela: o produto escolhido e quantas unidades dele vão no combo. */
interface Line {
  productId: number
  productName: string
  unitPrice: number
  currentStock: number
  quantity: string
}

export function ComboFormModal({ combo, onClose, onSaved }: ComboFormModalProps) {
  const [name, setName] = useState(combo?.name ?? '')
  const [description, setDescription] = useState(combo?.description ?? '')
  const [salePrice, setSalePrice] = useState(combo ? String(combo.salePrice).replace('.', ',') : '')
  const [active, setActive] = useState(combo?.active ?? true)
  const [lines, setLines] = useState<Line[]>(
    combo?.items.map((i) => ({
      productId: i.productId,
      productName: i.productName,
      unitPrice: i.unitPrice,
      currentStock: i.currentStock,
      quantity: String(i.quantity),
    })) ?? [],
  )
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  function addProduct(product: Product) {
    setLines((current) => [
      ...current,
      {
        productId: product.id,
        productName: product.name,
        unitPrice: product.salePrice,
        currentStock: product.currentStock,
        quantity: '1',
      },
    ])
  }

  const setQuantity = (productId: number, quantity: string) =>
    setLines((current) => current.map((l) => (l.productId === productId ? { ...l, quantity } : l)))

  const removeLine = (productId: number) => setLines((current) => current.filter((l) => l.productId !== productId))

  // As mesmas contas que a API faz, para o ADMIN ver a economia enquanto monta o combo.
  // A API confere tudo de novo ao salvar.
  const normalPrice = lines.reduce((sum, l) => sum + l.unitPrice * (Number(l.quantity) || 0), 0)
  const price = parseDecimal(salePrice)
  const hasPrice = salePrice.trim() !== '' && !Number.isNaN(price)
  const savings = hasPrice ? normalPrice - price : 0
  const tooExpensive = hasPrice && lines.length > 0 && price > normalPrice

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})

    const input = {
      name,
      description: emptyToNull(description),
      salePrice: hasPrice ? price : 0,
      items: lines.map((l) => ({ productId: l.productId, quantity: Number(l.quantity) || 0 })),
    }

    try {
      if (combo) {
        await combosApi.update(combo.id, { ...input, active })
        onSaved('Combo atualizado com sucesso.')
      } else {
        await combosApi.create(input)
        onSaved('Combo cadastrado com sucesso.')
      }
    } catch (err) {
      setError(getErrorMessage(err))
      setFieldErrors(getFieldErrors(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal show onHide={saving ? undefined : onClose} backdrop="static" keyboard={false} centered size="lg">
      <Form onSubmit={handleSubmit} noValidate>
        <Modal.Header closeButton closeLabel="Fechar">
          <Modal.Title as="h5">{combo ? 'Editar combo' : 'Novo combo'}</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger" className="py-2">{error}</Alert>}
          <Row className="g-3">
            <Col md={8}>
              <Form.Group controlId="combo-name">
                <Form.Label>Nome<RequiredMark /></Form.Label>
                <Form.Control
                  value={name}
                  maxLength={150}
                  autoFocus
                  placeholder="Ex.: Kit Churrasco"
                  onChange={(e) => setName(e.target.value)}
                  isInvalid={!!fieldErrors.name}
                />
                <Form.Control.Feedback type="invalid">{fieldErrors.name}</Form.Control.Feedback>
              </Form.Group>
            </Col>
            <Col md={4}>
              <Form.Group controlId="combo-price">
                <Form.Label>Preço do combo<RequiredMark /></Form.Label>
                <InputGroup hasValidation>
                  <InputGroup.Text>R$</InputGroup.Text>
                  <Form.Control
                    inputMode="decimal"
                    value={salePrice}
                    placeholder="0,00"
                    onChange={(e) => {
                      // Mudou o preço: o erro da última tentativa de salvar já não vale.
                      setSalePrice(e.target.value)
                      setError(null)
                      setFieldErrors((errors) => ({ ...errors, salePrice: '' }))
                    }}
                    isInvalid={!!fieldErrors.salePrice || tooExpensive}
                  />
                  <Form.Control.Feedback type="invalid">
                    {fieldErrors.salePrice || 'Mais caro que os produtos separados.'}
                  </Form.Control.Feedback>
                </InputGroup>
              </Form.Group>
            </Col>
            <Col xs={12}>
              <Form.Group controlId="combo-description">
                <Form.Label>Descrição</Form.Label>
                <Form.Control
                  value={description}
                  maxLength={500}
                  placeholder="Opcional, aparece para o vendedor no PDV"
                  onChange={(e) => setDescription(e.target.value)}
                  isInvalid={!!fieldErrors.description}
                />
                <Form.Control.Feedback type="invalid">{fieldErrors.description}</Form.Control.Feedback>
              </Form.Group>
            </Col>

            <Col xs={12}>
              <Form.Label htmlFor="combo-product">Produtos do combo<RequiredMark /></Form.Label>
              <ProductPicker
                id="combo-product"
                activeOnly
                excludeIds={lines.map((l) => l.productId)}
                onSelect={addProduct}
                isInvalid={!!fieldErrors.items}
              />
              {fieldErrors.items && <div className="invalid-feedback d-block">{fieldErrors.items}</div>}

              {lines.length > 0 && (
                <Table size="sm" responsive className="align-middle mt-2 mb-0">
                  <thead>
                    <tr>
                      <th>Produto</th>
                      <th style={{ width: 110 }}>Qtd.</th>
                      <th className="text-end">Preço avulso</th>
                      <th className="text-end">Subtotal</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {lines.map((line) => (
                      <tr key={line.productId}>
                        <td>
                          {line.productName}
                          <div className="small text-secondary">estoque {line.currentStock}</div>
                        </td>
                        <td>
                          <Form.Control
                            size="sm"
                            type="number"
                            min={1}
                            max={1000}
                            value={line.quantity}
                            aria-label={`Quantidade de ${line.productName}`}
                            onChange={(e) => setQuantity(line.productId, e.target.value)}
                          />
                        </td>
                        <td className="text-end text-nowrap">{formatCurrency(line.unitPrice)}</td>
                        <td className="text-end text-nowrap">
                          {formatCurrency(line.unitPrice * (Number(line.quantity) || 0))}
                        </td>
                        <td className="text-end">
                          <Button
                            size="sm"
                            variant="outline-danger"
                            aria-label={`Remover ${line.productName}`}
                            onClick={() => removeLine(line.productId)}
                            title="Remover"
                          >
                            <i className="bi bi-trash3" aria-hidden="true" />
                          </Button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              )}
            </Col>

            {lines.length > 0 && (
              <Col xs={12}>
                <div className="d-flex flex-wrap gap-3 small bg-body-tertiary rounded p-2">
                  <span>
                    Separados: <strong>{formatCurrency(normalPrice)}</strong>
                  </span>
                  {hasPrice && (
                    <span className={savings < 0 ? 'text-danger' : 'text-success'}>
                      {savings < 0 ? 'Fica mais caro: ' : 'Economia do cliente: '}
                      <strong>
                        {formatCurrency(Math.abs(savings))}
                        {normalPrice > 0 && ` (${formatPercent((Math.abs(savings) / normalPrice) * 100)})`}
                      </strong>
                    </span>
                  )}
                </div>
              </Col>
            )}

            {combo && (
              <Col xs={12}>
                <Form.Check
                  type="switch"
                  id="combo-active"
                  label="Ativo (aparece no PDV)"
                  checked={active}
                  onChange={(e) => setActive(e.target.checked)}
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
