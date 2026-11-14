import { useState } from 'react'
import { Button, ButtonGroup, Col, Form, Row } from 'react-bootstrap'
import type { Period } from '../types/report'
import { presetPeriod, type PeriodPreset } from '../utils/period'

const presets: { value: Exclude<PeriodPreset, 'custom'>; label: string }[] = [
  { value: 'today', label: 'Hoje' },
  { value: '7d', label: '7 dias' },
  { value: '30d', label: '30 dias' },
  { value: 'month', label: 'Mês atual' },
]

interface PeriodFilterProps {
  value: Period
  onChange: (period: Period) => void
}

/** Filtro de período dos relatórios: atalhos (Hoje, 7 dias, 30 dias, Mês atual) ou datas livres. */
export function PeriodFilter({ value, onChange }: PeriodFilterProps) {
  const [preset, setPreset] = useState<PeriodPreset>('30d')

  return (
    <Row className="g-2 align-items-end">
      <Col xs="auto">
        <ButtonGroup aria-label="Período" size="sm" className="flex-wrap">
          {presets.map((p) => (
            <Button
              key={p.value}
              variant={preset === p.value ? 'secondary' : 'outline-secondary'}
              className="text-nowrap"
              onClick={() => {
                setPreset(p.value)
                onChange(presetPeriod(p.value))
              }}
            >
              {p.label}
            </Button>
          ))}
          <Button variant={preset === 'custom' ? 'secondary' : 'outline-secondary'} onClick={() => setPreset('custom')}>
            Personalizado
          </Button>
        </ButtonGroup>
      </Col>
      {preset === 'custom' && (
        <>
          <Col xs={6} sm="auto">
            <Form.Group controlId="period-from">
              <Form.Label className="small mb-1">De</Form.Label>
              <Form.Control
                size="sm"
                type="date"
                value={value.from}
                max={value.to}
                onChange={(e) => e.target.value && onChange({ ...value, from: e.target.value })}
              />
            </Form.Group>
          </Col>
          <Col xs={6} sm="auto">
            <Form.Group controlId="period-to">
              <Form.Label className="small mb-1">Até</Form.Label>
              <Form.Control
                size="sm"
                type="date"
                value={value.to}
                min={value.from}
                onChange={(e) => e.target.value && onChange({ ...value, to: e.target.value })}
              />
            </Form.Group>
          </Col>
        </>
      )}
    </Row>
  )
}
