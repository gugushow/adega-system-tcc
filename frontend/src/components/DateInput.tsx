import { Form, InputGroup } from 'react-bootstrap'

interface DateInputProps {
  /** Texto à esquerda da caixa: "De" ou "Até". */
  label: string
  value: string
  onChange: (value: string) => void
  min?: string
  max?: string
}

/**
 * Campo de data com o rótulo grudado ("De", "Até"), para as duas caixas de um período não ficarem iguais.
 * min/max impedem escolher um "De" depois do "Até" (e vice-versa).
 */
export function DateInput({ label, value, onChange, min, max }: DateInputProps) {
  return (
    <InputGroup>
      {/* px-2: menos folga lateral para a data caber inteira em colunas estreitas. */}
      <InputGroup.Text className="px-2">{label}</InputGroup.Text>
      <Form.Control
        type="date"
        className="px-2"
        aria-label={label}
        value={value}
        min={min || undefined}
        max={max || undefined}
        onChange={(e) => onChange(e.target.value)}
      />
    </InputGroup>
  )
}
