import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AxiosError, type AxiosResponse } from 'axios'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { productsApi } from '../../api/products'
import { fakeScan } from '../../test/fakeScannerModal'
import { makeProduct, renderWithAuth } from '../../test/render'
import { PosPage } from './PosPage'

vi.mock('../../components/barcode/BarcodeScannerModal', async () => ({
  BarcodeScannerModal: (await import('../../test/fakeScannerModal')).FakeScannerModal,
}))
vi.mock('../../api/products', () => ({
  productsApi: {
    list: vi.fn(),
    getByBarcode: vi.fn(),
  },
}))
vi.mock('../../api/combos', () => ({ combosApi: { list: vi.fn().mockResolvedValue({ items: [] }) } }))
vi.mock('../../api/sales', () => ({ salesApi: { create: vi.fn() } }))
vi.mock('../../api/customers', () => ({ customersApi: { list: vi.fn(), loyalty: vi.fn() } }))

const getByBarcode = vi.mocked(productsApi.getByBarcode)

const notFound = () =>
  new AxiosError('Not Found', 'ERR_BAD_REQUEST', undefined, undefined, {
    status: 404,
    data: { success: false, message: 'Nenhum produto com esse código de barras.' },
  } as AxiosResponse)

async function scan(user: ReturnType<typeof userEvent.setup>, code: string) {
  fakeScan.code = code
  await user.click(screen.getByRole('button', { name: /Escanear/ }))
  await user.click(screen.getByRole('button', { name: 'Simular leitura' }))
}

beforeEach(() => {
  vi.mocked(productsApi.list).mockResolvedValue({ items: [], page: 1, pageSize: 12, totalItems: 0, totalPages: 0 })
  getByBarcode.mockReset()
})

describe('PosPage: leitura pela câmera', () => {
  it('busca o produto pelo código lido e adiciona ao carrinho pelo fluxo atual', async () => {
    const user = userEvent.setup()
    getByBarcode.mockResolvedValue(makeProduct({ id: 7, name: 'Heineken 350ml', salePrice: 6.5 }))
    renderWithAuth(<PosPage />, { role: 'VENDEDOR' })

    await scan(user, '7891234567895')

    expect(getByBarcode).toHaveBeenCalledWith('7891234567895')
    expect(await screen.findByRole('button', { name: 'Remover Heineken 350ml' })).toBeInTheDocument()
    expect(screen.getByLabelText('Quantidade de Heineken 350ml')).toHaveValue(1)
    expect(screen.getByText(/adicionado ao carrinho/)).toBeInTheDocument()
  })

  it('ler o mesmo produto de novo aumenta a quantidade, como um clique na lista', async () => {
    const user = userEvent.setup()
    getByBarcode.mockResolvedValue(makeProduct({ id: 7, name: 'Heineken 350ml' }))
    renderWithAuth(<PosPage />, { role: 'VENDEDOR' })

    await scan(user, '7891234567895')
    await screen.findByRole('button', { name: 'Remover Heineken 350ml' })
    await scan(user, '7891234567895')

    expect(screen.getByLabelText('Quantidade de Heineken 350ml')).toHaveValue(2)
  })

  it('respeita o estoque: sem unidades suficientes, não adiciona e avisa', async () => {
    const user = userEvent.setup()
    getByBarcode.mockResolvedValue(makeProduct({ id: 7, name: 'Heineken 350ml', currentStock: 1 }))
    renderWithAuth(<PosPage />, { role: 'VENDEDOR' })

    await scan(user, '7891234567895')
    await screen.findByRole('button', { name: 'Remover Heineken 350ml' })
    await scan(user, '7891234567895')

    expect(await screen.findByText(/tem só 1 unidade\(s\) em estoque/)).toBeInTheDocument()
    expect(screen.getByLabelText('Quantidade de Heineken 350ml')).toHaveValue(1)
  })

  it('produto inativo não entra no carrinho', async () => {
    const user = userEvent.setup()
    getByBarcode.mockResolvedValue(makeProduct({ id: 7, name: 'Vinho antigo', active: false }))
    renderWithAuth(<PosPage />, { role: 'VENDEDOR' })

    await scan(user, '7891234567895')

    expect(await screen.findByText(/está inativo e não pode ser vendido/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Remover Vinho antigo' })).not.toBeInTheDocument()
  })

  it('código inexistente mostra "Produto não encontrado." sem botão de cadastro para o vendedor', async () => {
    const user = userEvent.setup()
    getByBarcode.mockRejectedValue(notFound())
    renderWithAuth(<PosPage />, { role: 'VENDEDOR' })

    await scan(user, '0000000000000')

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Produto não encontrado.')
    expect(within(alert).queryByRole('button', { name: /Cadastrar produto/ })).not.toBeInTheDocument()
  })

  it('para o ADMIN, "Cadastrar produto" leva ao cadastro com o código preenchido', async () => {
    const user = userEvent.setup()
    getByBarcode.mockRejectedValue(notFound())
    renderWithAuth(<PosPage />, { role: 'ADMIN', route: '/vendas' })

    await scan(user, '7899999999995')
    await user.click(await screen.findByRole('button', { name: /Cadastrar produto/ }))

    expect(screen.getByTestId('location')).toHaveTextContent('/produtos?novo=1&codigo=7899999999995')
  })

  it('com itens no carrinho, pede confirmação antes de sair para o cadastro', async () => {
    const user = userEvent.setup()
    getByBarcode.mockResolvedValueOnce(makeProduct({ id: 7, name: 'Heineken 350ml' })).mockRejectedValueOnce(notFound())
    renderWithAuth(<PosPage />, { role: 'ADMIN', route: '/vendas' })

    await scan(user, '7891234567895')
    await screen.findByRole('button', { name: 'Remover Heineken 350ml' })
    await scan(user, '7899999999995')
    await user.click(await screen.findByRole('button', { name: /Cadastrar produto/ }))

    expect(await screen.findByText(/o carrinho atual será descartado/)).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/vendas')

    await user.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(screen.getByRole('button', { name: 'Remover Heineken 350ml' })).toBeInTheDocument()
  })
})
