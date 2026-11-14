import { useEffect, useState, type KeyboardEvent } from 'react'
import { Button, Form, InputGroup, ListGroup } from 'react-bootstrap'
import { customersApi } from '../api/customers'
import { useDebounce } from '../hooks/useDebounce'
import type { Customer } from '../types/customer'
import { formatCpf } from '../utils/format'
import './ProductPicker.css'

interface CustomerPickerProps {
  value: Customer | null
  onChange: (customer: Customer | null) => void
  /** Botão "Novo": abre o cadastro rápido. */
  onCreateNew: () => void
}

/** Busca de cliente ativo por nome, CPF ou telefone, com o mesmo visual do ProductPicker. */
export function CustomerPicker({ value, onChange, onCreateNew }: CustomerPickerProps) {
  const [text, setText] = useState('')
  const [results, setResults] = useState<Customer[]>([])
  const [open, setOpen] = useState(false)
  const search = useDebounce(text.trim(), 300)

  useEffect(() => {
    if (search.length < 2) return
    let ignore = false
    customersApi
      .list({ search, active: true, pageSize: 8 })
      .then((data) => !ignore && setResults(data.items))
      .catch(() => !ignore && setResults([]))
    return () => {
      ignore = true
    }
  }, [search])

  const visible = search.length >= 2 ? results : []

  function choose(customer: Customer) {
    onChange(customer)
    setText('')
    setOpen(false)
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      event.preventDefault()
      if (visible.length > 0) choose(visible[0])
    } else if (event.key === 'Escape' && open) {
      // Esc fecha só a lista de sugestões, sem fechar o modal ou a tela em volta.
      event.stopPropagation()
      setOpen(false)
    }
  }

  if (value) {
    return (
      <div className="d-flex justify-content-between align-items-center border rounded px-2 py-1">
        <div>
          <div className="fw-medium">{value.name}</div>
          {value.cpf && <small className="text-secondary font-monospace">{formatCpf(value.cpf)}</small>}
        </div>
        <Button size="sm" variant="outline-secondary" onClick={() => onChange(null)}>
          Remover
        </Button>
      </div>
    )
  }

  return (
    <div className="product-picker">
      <InputGroup>
        <Form.Control
          id="pos-customer"
          type="search"
          value={text}
          placeholder="Buscar por nome, CPF ou telefone (opcional)"
          aria-label="Cliente"
          autoComplete="off"
          onChange={(e) => {
            setText(e.target.value)
            setOpen(true)
          }}
          onFocus={() => setOpen(true)}
          onBlur={() => setTimeout(() => setOpen(false), 150)}
          onKeyDown={handleKeyDown}
        />
        <Button variant="outline-secondary" onClick={onCreateNew}>
          Novo
        </Button>
      </InputGroup>
      {open && visible.length > 0 && (
        <ListGroup className="product-picker-results shadow">
          {visible.map((customer) => (
            <ListGroup.Item key={customer.id} action onMouseDown={(e) => e.preventDefault()} onClick={() => choose(customer)}>
              <div>{customer.name}</div>
              <small className="text-secondary">
                {[customer.cpf && formatCpf(customer.cpf), customer.phone].filter(Boolean).join(' · ')}
              </small>
            </ListGroup.Item>
          ))}
        </ListGroup>
      )}
    </div>
  )
}
