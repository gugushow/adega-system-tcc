import { useEffect, useState, type FormEvent } from 'react'
import { Alert, Button, Card, Col, Form, InputGroup, Row } from 'react-bootstrap'
import { getErrorMessage, getFieldErrors } from '../../api/http'
import { settingsApi } from '../../api/settings'
import { Loading } from '../../components/Loading'
import { formatCurrency, parseDecimal } from '../../utils/format'

/** Mensagem de resultado dentro de cada cartão (cada configuração salva separado). */
type Feedback = { type: 'success' | 'danger'; message: string } | null

const toText = (value: number) => String(value).replace('.', ',')

/** Configurações do sistema (só ADMIN): margem mínima dos alertas e programa de fidelidade. */
export function SettingsPage() {
  return (
    <>
      <h1 className="h4 mb-3">Configurações</h1>
      <Row className="g-3">
        <Col lg={6} xl={5}>
          <MarginSettingsCard />
        </Col>
        <Col lg={6} xl={5}>
          <LoyaltySettingsCard />
        </Col>
      </Row>
    </>
  )
}

function MarginSettingsCard() {
  const [margin, setMargin] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [feedback, setFeedback] = useState<Feedback>(null)

  useEffect(() => {
    settingsApi
      .getMargin()
      .then((settings) => setMargin(toText(settings.minimumMarginPercent)))
      .catch((err) => setFeedback({ type: 'danger', message: getErrorMessage(err) }))
  }, [])

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (margin === null) return
    if (margin.trim() === '' || Number.isNaN(parseDecimal(margin))) {
      setError('Informe a margem mínima.')
      return
    }
    setSaving(true)
    setError(null)
    setFeedback(null)
    try {
      const saved = await settingsApi.updateMargin({ minimumMarginPercent: parseDecimal(margin) })
      setMargin(toText(saved.minimumMarginPercent))
      setFeedback({ type: 'success', message: 'Margem mínima atualizada.' })
    } catch (err) {
      const fields = getFieldErrors(err)
      if (fields.minimumMarginPercent) setError(fields.minimumMarginPercent)
      else setFeedback({ type: 'danger', message: getErrorMessage(err) })
    } finally {
      setSaving(false)
    }
  }

  return (
    <Card className="h-100">
      <Card.Header className="fw-semibold">Margem mínima</Card.Header>
      <Card.Body>
        {feedback && (
          <Alert variant={feedback.type} className="py-2" dismissible closeLabel="Fechar" onClose={() => setFeedback(null)}>
            {feedback.message}
          </Alert>
        )}
        {margin === null ? (
          feedback ? null : <Loading />
        ) : (
          <Form onSubmit={handleSubmit} noValidate>
            <Form.Group controlId="minimum-margin" className="mb-3">
              <Form.Label>Margem mínima desejada</Form.Label>
              <InputGroup hasValidation>
                <Form.Control
                  inputMode="decimal"
                  value={margin}
                  onChange={(e) => setMargin(e.target.value)}
                  isInvalid={!!error}
                />
                <InputGroup.Text>%</InputGroup.Text>
                <Form.Control.Feedback type="invalid">{error}</Form.Control.Feedback>
              </InputGroup>
              <Form.Text>
                Produtos com margem abaixo deste valor aparecem com o alerta "Margem abaixo do mínimo configurado." no
                cadastro de produtos e no relatório de margens.
              </Form.Text>
            </Form.Group>
            <Button type="submit" disabled={saving}>
              {saving ? 'Salvando...' : 'Salvar'}
            </Button>
          </Form>
        )}
      </Card.Body>
    </Card>
  )
}

/**
 * Regra do programa de fidelidade. Mudar a regra vale só para as próximas vendas:
 * os pontos já ganhos ficam como estão no extrato de cada cliente.
 */
function LoyaltySettingsCard() {
  const [enabled, setEnabled] = useState(true)
  const [amount, setAmount] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [feedback, setFeedback] = useState<Feedback>(null)

  useEffect(() => {
    settingsApi
      .getLoyalty()
      .then((settings) => {
        setEnabled(settings.enabled)
        setAmount(toText(settings.amountPerPoint))
      })
      .catch((err) => setFeedback({ type: 'danger', message: getErrorMessage(err) }))
  }, [])

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (amount === null) return
    if (amount.trim() === '' || Number.isNaN(parseDecimal(amount))) {
      setError('Informe quantos reais valem 1 ponto.')
      return
    }
    setSaving(true)
    setError(null)
    setFeedback(null)
    try {
      const saved = await settingsApi.updateLoyalty({ enabled, amountPerPoint: parseDecimal(amount) })
      setEnabled(saved.enabled)
      setAmount(toText(saved.amountPerPoint))
      setFeedback({ type: 'success', message: 'Programa de fidelidade atualizado.' })
    } catch (err) {
      const fields = getFieldErrors(err)
      if (fields.amountPerPoint) setError(fields.amountPerPoint)
      else setFeedback({ type: 'danger', message: getErrorMessage(err) })
    } finally {
      setSaving(false)
    }
  }

  const preview = amount !== null ? parseDecimal(amount) : NaN

  return (
    <Card className="h-100">
      <Card.Header className="fw-semibold">Programa de fidelidade</Card.Header>
      <Card.Body>
        {feedback && (
          <Alert variant={feedback.type} className="py-2" dismissible closeLabel="Fechar" onClose={() => setFeedback(null)}>
            {feedback.message}
          </Alert>
        )}
        {amount === null ? (
          feedback ? null : <Loading />
        ) : (
          <Form onSubmit={handleSubmit} noValidate>
            <Form.Check
              type="switch"
              id="loyalty-enabled"
              className="mb-3"
              label="Programa ligado (vendas com cliente geram pontos)"
              checked={enabled}
              onChange={(e) => setEnabled(e.target.checked)}
            />
            <Form.Group controlId="loyalty-amount" className="mb-3">
              <Form.Label>Valor em compras para ganhar 1 ponto</Form.Label>
              <InputGroup hasValidation>
                <InputGroup.Text>R$</InputGroup.Text>
                <Form.Control
                  inputMode="decimal"
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  isInvalid={!!error}
                />
                <Form.Control.Feedback type="invalid">{error}</Form.Control.Feedback>
              </InputGroup>
              <Form.Text>
                {preview > 0
                  ? `Exemplo: uma venda de ${formatCurrency(preview * 5 + preview / 2)} dá 5 pontos (sempre arredonda para baixo). `
                  : ''}
                A mudança vale para as próximas vendas; os pontos já ganhos não mudam.
              </Form.Text>
            </Form.Group>
            <Button type="submit" disabled={saving}>
              {saving ? 'Salvando...' : 'Salvar'}
            </Button>
          </Form>
        )}
      </Card.Body>
    </Card>
  )
}
