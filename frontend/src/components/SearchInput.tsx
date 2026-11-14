import type { ReactNode } from 'react'
import { Form, InputGroup } from 'react-bootstrap'

interface SearchInputProps {
  value: string
  onChange: (value: string) => void
  placeholder?: string
  /** Botão no fim do campo (ex.: "Escanear código" na tela de Produtos). */
  action?: ReactNode
}

export function SearchInput({ value, onChange, placeholder = 'Buscar...', action }: SearchInputProps) {
  return (
    <InputGroup>
      <InputGroup.Text aria-hidden>
        <i className="bi bi-search" />
      </InputGroup.Text>
      <Form.Control
        type="search"
        value={value}
        placeholder={placeholder}
        aria-label={placeholder}
        onChange={(event) => onChange(event.target.value)}
      />
      {action}
    </InputGroup>
  )
}
