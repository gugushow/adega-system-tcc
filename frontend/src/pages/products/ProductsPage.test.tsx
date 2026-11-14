import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AxiosError, type AxiosResponse } from 'axios'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { productsApi } from '../../api/products'
import { fakeScan } from '../../test/fakeScannerModal'
import { makeProduct, renderWithAuth } from '../../test/render'
import { ProductsPage } from './ProductsPage'

vi.mock('../../components/barcode/BarcodeScannerModal', async () => ({
  BarcodeScannerModal: (await import('../../test/fakeScannerModal')).FakeScannerModal,
}))
vi.mock('../../api/products', () => ({
  productsApi: { list: vi.fn(), getByBarcode: vi.fn() },
}))
vi.mock('../../api/categories', () => ({ categoriesApi: { listActive: vi.fn().mockResolvedValue([]) } }))
vi.mock('../../hooks/useMinimumMargin', () => ({ useMinimumMargin: () => null }))

const list = vi.mocked(productsApi.list)
const getByBarcode = vi.mocked(productsApi.getByBarcode)

const page = (items = [makeProduct()]) => ({ items, page: 1, pageSize: 10, totalItems: items.length, totalPages: 1 })

const notFound = () =>
  new AxiosError('Not Found', 'ERR_BAD_REQUEST', undefined, undefined, {
    status: 404,
    data: { success: false, message: 'Nenhum produto com esse código de barras.' },
  } as AxiosResponse)

async function scan(user: ReturnType<typeof userEvent.setup>, code: string) {
  fakeScan.code = code
  await user.click(screen.getByRole('button', { name: 'Escanear código de barras' }))
  await user.click(screen.getByRole('button', { name: 'Simular leitura' }))
}

beforeEach(() => {
  list.mockReset().mockResolvedValue(page([]))
  getByBarcode.mockReset()
})

describe('ProductsPage: cadastro vindo do PDV (?novo=1&codigo=...)', () => {
  it('abre o formulário de novo produto com o código preenchido para quem pode cadastrar', async () => {
    renderWithAuth(<ProductsPage />, { role: 'ESTOQUISTA', route: '/produtos?novo=1&codigo=7891234567895' })

    expect(await screen.findByRole('heading', { name: 'Novo produto' })).toBeInTheDocument()
    expect(screen.getByLabelText('Código de barras')).toHaveValue('7891234567895')
    // O endereço é limpo: atualizar a página não reabre o formulário.
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(/^\/produtos$/))
  })

  it('não abre o formulário para o vendedor, mesmo com o link', async () => {
    renderWithAuth(<ProductsPage />, { role: 'VENDEDOR', route: '/produtos?novo=1&codigo=7891234567895' })

    expect(await screen.findByRole('heading', { name: 'Produtos' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Novo produto' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Novo produto/ })).not.toBeInTheDocument()
  })
})

describe('ProductsPage: escanear código na pesquisa', () => {
  it('localiza o produto lido: busca pelo código salvo e mostra a mensagem', async () => {
    const user = userEvent.setup()
    // Lido como EAN-13; a API acha o produto salvo como UPC-A (a equivalência fica no backend).
    getByBarcode.mockResolvedValue(makeProduct({ name: 'Heineken 350ml', barcode: '036000291452' }))
    renderWithAuth(<ProductsPage />, { role: 'VENDEDOR' })
    await screen.findByRole('heading', { name: 'Produtos' })

    await scan(user, '0036000291452')

    expect(getByBarcode).toHaveBeenCalledWith('0036000291452')
    expect(await screen.findByText('Produto encontrado: Heineken 350ml.')).toBeInTheDocument()
    expect(screen.getByRole('searchbox')).toHaveValue('036000291452')
    await waitFor(() =>
      expect(list).toHaveBeenLastCalledWith(expect.objectContaining({ search: '036000291452', active: true, page: 1 })),
    )
  })

  it('limpa filtros que esconderiam o produto e mostra inativos quando ele está inativo', async () => {
    const user = userEvent.setup()
    getByBarcode.mockResolvedValue(makeProduct({ name: 'Vinho antigo', barcode: '7891234567895', active: false }))
    renderWithAuth(<ProductsPage />, { role: 'ESTOQUISTA', route: '/produtos?estoque=Low' })
    await screen.findByRole('heading', { name: 'Produtos' })

    await scan(user, '7891234567895')

    expect(await screen.findByText('Produto encontrado: Vinho antigo (inativo).')).toBeInTheDocument()
    expect(screen.getByLabelText('Situação')).toHaveValue('all')
    expect(screen.getByLabelText('Estoque')).toHaveValue('')
    await waitFor(() =>
      expect(list).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: '7891234567895', active: undefined, stock: undefined, categoryId: undefined }),
      ),
    )
  })

  it('código inexistente: "Produto não encontrado." sem cadastro para o vendedor', async () => {
    const user = userEvent.setup()
    getByBarcode.mockRejectedValue(notFound())
    renderWithAuth(<ProductsPage />, { role: 'VENDEDOR' })
    await screen.findByRole('heading', { name: 'Produtos' })

    await scan(user, '7899999999995')

    expect(await screen.findByText('Produto não encontrado.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Cadastrar produto/ })).not.toBeInTheDocument()
    expect(screen.getByRole('searchbox')).toHaveValue('') // a busca manual continua como estava
  })

  it('código inexistente: o estoquista cadastra pelo mesmo formulário, com o código preenchido', async () => {
    const user = userEvent.setup()
    getByBarcode.mockRejectedValue(notFound())
    renderWithAuth(<ProductsPage />, { role: 'ESTOQUISTA' })
    await screen.findByRole('heading', { name: 'Produtos' })

    await scan(user, '7899999999995')
    await user.click(await screen.findByRole('button', { name: /Cadastrar produto/ }))

    expect(await screen.findByRole('heading', { name: 'Novo produto' })).toBeInTheDocument()
    expect(screen.getByLabelText('Código de barras')).toHaveValue('7899999999995')
  })

  it('a busca manual continua funcionando', async () => {
    const user = userEvent.setup()
    renderWithAuth(<ProductsPage />, { role: 'VENDEDOR' })
    await screen.findByRole('heading', { name: 'Produtos' })

    await user.type(screen.getByRole('searchbox'), 'heineken')

    await waitFor(() => expect(list).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'heineken' })))
    expect(getByBarcode).not.toHaveBeenCalled()
  })
})
