import { useEffect, useState } from 'react'
import { Badge, Button, Card, Col, Form, Row, Table } from 'react-bootstrap'
import { useSearchParams } from 'react-router'
import { categoriesApi } from '../../api/categories'
import { getErrorMessage } from '../../api/http'
import { productsApi } from '../../api/products'
import { access } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { ActionButton } from '../../components/ActionButton'
import { AppAlert, type Feedback } from '../../components/AppAlert'
import { BarcodeScannerModal } from '../../components/barcode/BarcodeScannerModal'
import { findProductByBarcode } from '../../components/barcode/productLookup'
import { ProductNotFoundAlert } from '../../components/barcode/ProductNotFoundAlert'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { EmptyState } from '../../components/EmptyState'
import { Loading } from '../../components/Loading'
import { Pagination } from '../../components/Pagination'
import { SearchInput } from '../../components/SearchInput'
import { useDebounce } from '../../hooks/useDebounce'
import { useMinimumMargin } from '../../hooks/useMinimumMargin'
import type { PagedResult } from '../../types/api'
import type { Category } from '../../types/category'
import type { Product, StockFilter } from '../../types/product'
import { formatCurrency, formatPercent } from '../../utils/format'
import { CostHistoryModal } from './CostHistoryModal'
import { ProductFormModal } from './ProductFormModal'
import { StockBadge } from './StockBadge'

type ActiveFilter = 'true' | 'false' | 'all'

export function ProductsPage() {
  const { hasRole } = useAuth()
  // O vendedor só consulta: cadastro, edição e histórico de custos são do ADMIN e do ESTOQUISTA.
  const canEdit = hasRole(access.catalogWrite)
  const minimumMargin = useMinimumMargin()
  const [search, setSearch] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [activeFilter, setActiveFilter] = useState<ActiveFilter>('true')
  // ?estoque=Low|Out: os alertas do dashboard abrem a lista já filtrada.
  const [searchParams, setSearchParams] = useSearchParams()
  const [stock, setStock] = useState<StockFilter | ''>(() => {
    const value = searchParams.get('estoque')
    return value === 'Low' || value === 'Out' ? value : ''
  })
  // ?novo=1&codigo=...: o PDV leu um código que não existe e quem pode cadastrar escolheu "Cadastrar produto".
  // Só abre o formulário para quem tem permissão; de qualquer forma, o backend recusa o cadastro dos outros.
  const [openNewFromLink] = useState(() => canEdit && searchParams.get('novo') === '1')
  // Vale só para esse primeiro formulário: um "Novo produto" depois abre vazio.
  const [linkBarcode, setLinkBarcode] = useState(() => (openNewFromLink ? (searchParams.get('codigo')?.trim() ?? '') : ''))
  const [page, setPage] = useState(1)

  const [categories, setCategories] = useState<Category[]>([])
  const [result, setResult] = useState<PagedResult<Product> | null>(null)
  // true só até a primeira resposta; depois a tabela antiga fica visível enquanto a nova busca carrega.
  const [loading, setLoading] = useState(true)
  const [feedback, setFeedback] = useState<Feedback | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  const [editing, setEditing] = useState<Product | null | undefined>(openNewFromLink ? null : undefined)
  const [toDeactivate, setToDeactivate] = useState<Product | null>(null)
  const [viewingCosts, setViewingCosts] = useState<Product | null>(null)
  const [deactivating, setDeactivating] = useState(false)
  const [scanning, setScanning] = useState(false)
  const [notFoundCode, setNotFoundCode] = useState<string | null>(null)

  const debouncedSearch = useDebounce(search)

  // Tira novo/codigo do endereço depois de usar: atualizar a página não reabre o formulário.
  useEffect(() => {
    if (!searchParams.has('novo') && !searchParams.has('codigo')) return
    setSearchParams(
      (params) => {
        params.delete('novo')
        params.delete('codigo')
        return params
      },
      { replace: true },
    )
  }, [searchParams, setSearchParams])

  // Categorias para o filtro e para o formulário: carregadas uma vez.
  useEffect(() => {
    categoriesApi
      .listActive()
      .then(setCategories)
      .catch((err) => setFeedback({ type: 'danger', message: getErrorMessage(err) }))
  }, [])

  useEffect(() => {
    let ignore = false
    productsApi
      .list({
        search: debouncedSearch || undefined,
        categoryId: categoryId ? Number(categoryId) : undefined,
        active: activeFilter === 'all' ? undefined : activeFilter === 'true',
        stock: stock || undefined,
        page,
        pageSize: 10,
      })
      .then((data) => !ignore && setResult(data))
      .catch((err) => !ignore && setFeedback({ type: 'danger', message: getErrorMessage(err) }))
      .finally(() => !ignore && setLoading(false))
    return () => {
      ignore = true
    }
  }, [debouncedSearch, categoryId, activeFilter, stock, page, reloadKey])

  const reload = () => setReloadKey((key) => key + 1)

  /** Muda um filtro e volta para a página 1. */
  const changeFilter = (apply: () => void) => {
    apply()
    setPage(1)
  }

  function closeForm() {
    setEditing(undefined)
    setLinkBarcode('')
  }

  /** "Cadastrar produto" depois de ler um código que não existe: o mesmo formulário, com o código preenchido. */
  function openNewWithBarcode(code: string) {
    setNotFoundCode(null)
    setLinkBarcode(code)
    setEditing(null)
  }

  /**
   * Código lido pela câmera: a API acha o produto (inclusive pela forma UPC-A/EAN-13 equivalente) e a lista
   * passa a mostrar só ele, buscando pelo código salvo. Filtros que o esconderiam são limpos.
   */
  async function handleScanned(code: string) {
    setScanning(false)
    setNotFoundCode(null)
    const lookup = await findProductByBarcode(code)
    if (lookup.kind === 'not-found') {
      setNotFoundCode(code)
      return
    }
    if (lookup.kind === 'error') {
      setFeedback({ type: 'danger', message: lookup.message })
      return
    }
    const { product } = lookup
    changeFilter(() => {
      setSearch(product.barcode ?? code)
      setCategoryId('')
      setStock('')
      if (!product.active) setActiveFilter('all')
    })
    setFeedback({ type: 'success', message: `Produto encontrado: ${product.name}${product.active ? '' : ' (inativo)'}.` })
  }

  function handleSaved(message: string) {
    closeForm()
    setFeedback({ type: 'success', message })
    reload()
  }

  async function handleDeactivate() {
    if (!toDeactivate) return
    setDeactivating(true)
    try {
      await productsApi.deactivate(toDeactivate.id)
      setFeedback({ type: 'success', message: 'Produto desativado com sucesso.' })
      reload()
    } catch (err) {
      setFeedback({ type: 'danger', message: getErrorMessage(err) })
    } finally {
      setDeactivating(false)
      setToDeactivate(null)
    }
  }

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="h4 mb-0">Produtos</h1>
        {canEdit && (
          <Button onClick={() => setEditing(null)}>
            <i className="bi bi-plus-lg me-1" aria-hidden="true" />
            Novo produto
          </Button>
        )}
      </div>

      <AppAlert feedback={feedback} onClose={() => setFeedback(null)} />
      {notFoundCode !== null && (
        <ProductNotFoundAlert code={notFoundCode} onCreateProduct={openNewWithBarcode} onClose={() => setNotFoundCode(null)} />
      )}

      <Card>
        <Card.Body>
          <Row className="g-2 mb-3">
            <Col lg={5}>
              <SearchInput
                value={search}
                placeholder="Buscar por nome, marca ou código de barras"
                onChange={(value) => changeFilter(() => setSearch(value))}
                action={
                  <Button
                    variant="outline-secondary"
                    onClick={() => setScanning(true)}
                    aria-label="Escanear código de barras"
                    title="Localizar o produto lendo o código de barras com a câmera"
                  >
                    <i className="bi bi-upc-scan me-xl-1" aria-hidden="true" />
                    {/* Texto só em tela larga: no resto, o ícone deixa espaço para o campo de busca. */}
                    <span className="d-none d-xl-inline" aria-hidden="true">
                      Escanear código
                    </span>
                  </Button>
                }
              />
            </Col>
            <Col sm={4} lg={3}>
              <Form.Select
                aria-label="Categoria"
                value={categoryId}
                onChange={(e) => changeFilter(() => setCategoryId(e.target.value))}
              >
                <option value="">Todas as categorias</option>
                {categories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </Form.Select>
            </Col>
            <Col sm={4} lg={2}>
              <Form.Select
                aria-label="Estoque"
                value={stock}
                onChange={(e) => changeFilter(() => setStock(e.target.value as StockFilter | ''))}
              >
                <option value="">Todo estoque</option>
                <option value="Low">Estoque baixo</option>
                <option value="Out">Sem estoque</option>
              </Form.Select>
            </Col>
            <Col sm={4} lg={2}>
              <Form.Select
                aria-label="Situação"
                value={activeFilter}
                onChange={(e) => changeFilter(() => setActiveFilter(e.target.value as ActiveFilter))}
              >
                <option value="true">Ativos</option>
                <option value="false">Inativos</option>
                <option value="all">Todos</option>
              </Form.Select>
            </Col>
          </Row>

          {loading && !result ? (
            <Loading />
          ) : result && result.items.length === 0 ? (
            <EmptyState
              title="Nenhum produto encontrado"
              description={canEdit ? 'Ajuste os filtros ou cadastre um novo produto.' : 'Ajuste os filtros da busca.'}
            />
          ) : (
            result && (
              <>
                <Table responsive hover className="align-middle">
                  <thead>
                    <tr>
                      <th>Produto</th>
                      <th className="d-none d-md-table-cell">Código</th>
                      <th className="d-none d-md-table-cell">Categoria</th>
                      <th className="d-none d-md-table-cell text-end">Custo</th>
                      <th className="text-end">Venda</th>
                      <th className="d-none d-md-table-cell text-end">Margem</th>
                      <th className="text-end">Estoque</th>
                      <th className="d-none d-md-table-cell">Situação</th>
                      {canEdit && <th className="text-end">Ações</th>}
                    </tr>
                  </thead>
                  <tbody>
                    {result.items.map((product) => (
                      <tr key={product.id}>
                        <td>
                          <div className="fw-medium">{product.name}</div>
                          {/* Marca e volume só quando existem (sem um "—" solto embaixo do nome). */}
                          {(product.brand || product.volume) && (
                            <small className="text-secondary">
                              {[product.brand, product.volume].filter(Boolean).join(' · ')}
                            </small>
                          )}
                        </td>
                        <td className="d-none d-md-table-cell font-monospace small">{product.barcode ?? '—'}</td>
                        <td className="d-none d-md-table-cell">{product.categoryName}</td>
                        <td className="d-none d-md-table-cell text-end">{formatCurrency(product.costPrice)}</td>
                        <td className="text-end">{formatCurrency(product.salePrice)}</td>
                        <td className="d-none d-md-table-cell text-end">
                          {minimumMargin !== null && product.marginPercent < minimumMargin ? (
                            <Badge bg="danger" title="Margem abaixo do mínimo configurado.">
                              {formatPercent(product.marginPercent)}
                            </Badge>
                          ) : (
                            formatPercent(product.marginPercent)
                          )}
                        </td>
                        <td className="text-end">
                          <StockBadge product={product} />
                          <div>
                            <small className="text-secondary">mín. {product.minimumStock}</small>
                          </div>
                        </td>
                        <td className="d-none d-md-table-cell">
                          <Badge bg={product.active ? 'success' : 'secondary'}>
                            {product.active ? 'Ativo' : 'Inativo'}
                          </Badge>
                        </td>
                        {canEdit && (
                          <td className="table-actions">
                            <ActionButton icon="bi-graph-up" label="Custos" onClick={() => setViewingCosts(product)} />
                            <ActionButton icon="bi-pencil" label="Editar" onClick={() => setEditing(product)} />
                            {product.active && (
                              <ActionButton
                                icon="bi-slash-circle"
                                label="Desativar"
                                variant="outline-danger"
                                onClick={() => setToDeactivate(product)}
                              />
                            )}
                          </td>
                        )}
                      </tr>
                    ))}
                  </tbody>
                </Table>
                <Pagination
                  page={result.page}
                  totalPages={result.totalPages}
                  totalItems={result.totalItems}
                  onPageChange={setPage}
                />
              </>
            )
          )}
        </Card.Body>
      </Card>

      {editing !== undefined && (
        <ProductFormModal
          product={editing}
          categories={categories}
          minimumMargin={minimumMargin}
          initialBarcode={editing === null ? linkBarcode : undefined}
          onClose={closeForm}
          onSaved={handleSaved}
        />
      )}

      {viewingCosts && <CostHistoryModal product={viewingCosts} onClose={() => setViewingCosts(null)} />}

      <BarcodeScannerModal show={scanning} onDetected={handleScanned} onClose={() => setScanning(false)} />

      <ConfirmDialog
        show={toDeactivate !== null}
        title="Desativar produto"
        message={`Deseja desativar "${toDeactivate?.name}"? Ele sai das listagens de venda, mas o histórico é mantido.`}
        confirmLabel="Desativar"
        busy={deactivating}
        onConfirm={handleDeactivate}
        onCancel={() => setToDeactivate(null)}
      />
    </>
  )
}
