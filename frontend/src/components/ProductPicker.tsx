import { useEffect, useState, type KeyboardEvent } from 'react'
import { Form, ListGroup } from 'react-bootstrap'
import { productsApi } from '../api/products'
import { useDebounce } from '../hooks/useDebounce'
import type { Product } from '../types/product'
import './ProductPicker.css'

interface ProductPickerProps {
  onSelect: (product: Product) => void
  /** Só produtos ativos (compras) ou todos (filtros e ajustes). */
  activeOnly?: boolean
  /** Produtos que não devem aparecer, como os que já estão na compra. */
  excludeIds?: number[]
  placeholder?: string
  isInvalid?: boolean
  id?: string
  autoFocus?: boolean
  /**
   * Quando informado, a lista ganha a opção "Cadastrar novo produto" (com o texto buscado),
   * para quem tem permissão de cadastrar. Usado na Nova compra.
   */
  onCreateNew?: (text: string) => void
}

/**
 * Campo de busca de produto com lista de sugestões. Busca por nome, marca ou código de barras
 * (o mesmo filtro "search" da tela de produtos). Com um leitor de código de barras,
 * o leitor "digita" o código e aperta Enter: o primeiro resultado é escolhido.
 */
export function ProductPicker({
  onSelect,
  activeOnly = false,
  excludeIds = [],
  placeholder = 'Buscar produto por nome ou código de barras',
  isInvalid,
  id,
  autoFocus,
  onCreateNew,
}: ProductPickerProps) {
  const [text, setText] = useState('')
  // Guarda para qual busca os resultados vieram: evita dizer "nenhum produto" antes de a resposta chegar.
  const [results, setResults] = useState<{ search: string; items: Product[] }>({ search: '', items: [] })
  const [open, setOpen] = useState(false)
  const search = useDebounce(text.trim(), 300)

  useEffect(() => {
    if (search.length < 2) return
    let ignore = false
    productsApi
      .list({ search, active: activeOnly ? true : undefined, pageSize: 8 })
      .then((data) => !ignore && setResults({ search, items: data.items }))
      .catch(() => !ignore && setResults({ search, items: [] }))
    return () => {
      ignore = true
    }
  }, [search, activeOnly])

  // Com menos de 2 letras não mostramos sugestões (evita listar o catálogo inteiro).
  const loaded = search.length >= 2 && results.search === search
  const visible = loaded ? results.items.filter((p) => !excludeIds.includes(p.id)) : []
  const showCreate = !!onCreateNew && loaded

  function choose(product: Product) {
    onSelect(product)
    setText('')
    setOpen(false)
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      // Enter não pode enviar o formulário da página: ele escolhe o primeiro produto da lista.
      event.preventDefault()
      if (visible.length > 0) choose(visible[0])
    } else if (event.key === 'Escape' && open) {
      // Esc fecha só a lista de sugestões, sem fechar o modal ou a tela em volta.
      event.stopPropagation()
      setOpen(false)
    }
  }

  return (
    <div className="product-picker">
      <Form.Control
        id={id}
        type="search"
        value={text}
        placeholder={placeholder}
        aria-label={placeholder}
        autoComplete="off"
        isInvalid={isInvalid}
        autoFocus={autoFocus}
        onChange={(e) => {
          setText(e.target.value)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        // Pequeno atraso para o clique na sugestão acontecer antes de a lista fechar.
        onBlur={() => setTimeout(() => setOpen(false), 150)}
        onKeyDown={handleKeyDown}
      />
      {open && (visible.length > 0 || showCreate) && (
        <ListGroup className="product-picker-results shadow">
          {visible.length === 0 && (
            <ListGroup.Item className="text-secondary small">Nenhum produto encontrado para "{search}".</ListGroup.Item>
          )}
          {visible.map((product) => (
            <ListGroup.Item key={product.id} action onMouseDown={(e) => e.preventDefault()} onClick={() => choose(product)}>
              <div className="d-flex justify-content-between gap-2">
                <span>
                  {product.name}
                  {!product.active && <small className="text-secondary"> (inativo)</small>}
                </span>
                <small className="text-secondary text-nowrap">estoque {product.currentStock}</small>
              </div>
              {product.barcode && <small className="text-secondary font-monospace">{product.barcode}</small>}
            </ListGroup.Item>
          ))}
          {showCreate && (
            <ListGroup.Item
              action
              className="product-picker-create"
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => {
                setOpen(false)
                onCreateNew(search)
              }}
            >
              <i className="bi bi-plus-circle me-2" aria-hidden="true" />
              Cadastrar novo produto
            </ListGroup.Item>
          )}
        </ListGroup>
      )}
    </div>
  )
}
