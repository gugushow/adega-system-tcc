import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AxiosError, type AxiosResponse } from 'axios'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { batchesApi } from '../../api/batches'
import { productsApi } from '../../api/products'
import { fakeScan } from '../../test/fakeScannerModal'
import { makeProduct, renderWithAuth } from '../../test/render'
import type { Batch } from '../../types/batch'
import { QuickProductLookup } from './QuickProductLookup'

vi.mock('./BarcodeScannerModal', async () => ({
  BarcodeScannerModal: (await import('../../test/fakeScannerModal')).FakeScannerModal,
}))
vi.mock('../../api/products', () => ({ productsApi: { getByBarcode: vi.fn() } }))
vi.mock('../../api/batches', () => ({ batchesApi: { list: vi.fn() } }))

const getByBarcode = vi.mocked(productsApi.getByBarcode)
const listBatches = vi.mocked(batchesApi.list)

function makeBatch(overrides: Partial<Batch> = {}): Batch {
  return {
    id: 1,
    productId: 7,
    productName: 'Heineken 350ml',
    batchNumber: 'L-001',
    purchaseId: 1,
    supplierName: 'Distribuidora',
    quantityReceived: 24,
    quantityAvailable: 10,
    manufacturingDate: null,
    expirationDate: '2026-10-20',
    daysToExpire: 16,
    isExpired: false,
    unitCost: 3.9,
    createdAt: '2026-10-01T00:00:00Z',
    ...overrides,
  }
}

const notFound = () =>
  new AxiosError('Not Found', 'ERR_BAD_REQUEST', undefined, undefined, {
    status: 404,
    data: { success: false, message: 'Nenhum produto com esse código de barras.' },
  } as AxiosResponse)

const product = makeProduct({
  id: 7,
  name: 'Heineken 350ml',
  brand: 'Heineken',
  volume: '350ml',
  categoryName: 'Cerveja',
  barcode: '7891234567895',
  salePrice: 6.5,
  costPrice: 3.21,
  marginPercent: 50.6,
  currentStock: 4,
  minimumStock: 10,
  isLowStock: true,
})

async function openAndScan(user: ReturnType<typeof userEvent.setup>, code = '7891234567895') {
  fakeScan.code = code
  await user.click(screen.getByRole('button', { name: /Consultar produto/ }))
  await user.click(screen.getByRole('button', { name: 'Simular leitura' }))
}

beforeEach(() => {
  getByBarcode.mockReset()
  listBatches.mockReset().mockResolvedValue({ items: [], page: 1, pageSize: 3, totalItems: 0, totalPages: 0 })
})

describe('QuickProductLookup', () => {
  it('só abre o leitor depois do clique e busca o produto pelo código lido', async () => {
    const user = userEvent.setup()
    getByBarcode.mockResolvedValue(product)
    renderWithAuth(<QuickProductLookup />, { role: 'VENDEDOR' })
    expect(screen.queryByRole('dialog', { name: 'Leitor de código de barras' })).not.toBeInTheDocument()

    await openAndScan(user)

    expect(getByBarcode).toHaveBeenCalledWith('7891234567895')
    expect(screen.queryByRole('dialog', { name: 'Leitor de código de barras' })).not.toBeInTheDocument()
    expect(await screen.findByRole('dialog', { name: 'Heineken 350ml' })).toBeInTheDocument()
  })

  it('mostra preço, estoque e dados úteis, sem custo nem margem', async () => {
    const user = userEvent.setup()
    getByBarcode.mockResolvedValue(product)
    renderWithAuth(<QuickProductLookup />, { role: 'ADMIN' })

    await openAndScan(user)
    const dialog = await screen.findByRole('dialog', { name: 'Heineken 350ml' })

    expect(within(dialog).getByText(/^R\$\s6,50$/)).toBeInTheDocument() // Intl usa espaço não separável
    expect(within(dialog).getByText('4')).toBeInTheDocument()
    expect(within(dialog).getByText('Estoque baixo')).toBeInTheDocument()
    expect(within(dialog).getByText('Heineken · 350ml')).toBeInTheDocument()
    expect(within(dialog).getByText('Cerveja')).toBeInTheDocument()
    expect(within(dialog).getByText('7891234567895')).toBeInTheDocument()
    expect(within(dialog).getByText('10')).toBeInTheDocument() // estoque mínimo
    expect(dialog).not.toHaveTextContent(/custo/i)
    expect(dialog).not.toHaveTextContent(/margem/i)
    expect(dialog).not.toHaveTextContent('3,21')
  })

  it('ADMIN e ESTOQUISTA veem os próximos lotes e o alerta de lote vencido', async () => {
    const user = userEvent.setup()
    getByBarcode.mockResolvedValue(product)
    listBatches.mockResolvedValue({
      items: [
        makeBatch({ id: 1, batchNumber: 'L-VENC', expirationDate: '2026-09-30', daysToExpire: -4, isExpired: true }),
        makeBatch({ id: 2, batchNumber: 'L-002' }),
      ],
      page: 1,
      pageSize: 3,
      totalItems: 5,
      totalPages: 2,
    })
    renderWithAuth(<QuickProductLookup />, { role: 'ESTOQUISTA' })

    await openAndScan(user)
    const dialog = await screen.findByRole('dialog', { name: 'Heineken 350ml' })

    expect(await within(dialog).findByText('L-VENC')).toBeInTheDocument()
    expect(listBatches).toHaveBeenCalledWith({ productId: 7, pageSize: 3 })
    expect(within(dialog).getByText(/Há lote vencido com saldo/)).toBeInTheDocument()
    expect(within(dialog).getByText('30/09/2026 · vencido')).toBeInTheDocument()
    expect(within(dialog).getByText(/e mais 3 lote\(s\)/)).toBeInTheDocument()
  })

  it('o VENDEDOR não vê lotes (nem a requisição é feita)', async () => {
    const user = userEvent.setup()
    getByBarcode.mockResolvedValue(product)
    renderWithAuth(<QuickProductLookup />, { role: 'VENDEDOR' })

    await openAndScan(user)
    const dialog = await screen.findByRole('dialog', { name: 'Heineken 350ml' })

    expect(within(dialog).queryByText('Próximos lotes')).not.toBeInTheDocument()
    expect(listBatches).not.toHaveBeenCalled()
  })

  it('avisa quando o produto está inativo', async () => {
    const user = userEvent.setup()
    getByBarcode.mockResolvedValue({ ...product, active: false })
    renderWithAuth(<QuickProductLookup />, { role: 'VENDEDOR' })

    await openAndScan(user)

    expect(await screen.findByText(/Produto inativo/)).toBeInTheDocument()
  })

  it.each([
    ['o X do cabeçalho', 0],
    ['o botão Fechar do rodapé', 1],
  ])('fecha o modal por %s', async (_, index) => {
    const user = userEvent.setup()
    getByBarcode.mockResolvedValue(product)
    renderWithAuth(<QuickProductLookup />, { role: 'VENDEDOR' })

    await openAndScan(user)
    const dialog = await screen.findByRole('dialog', { name: 'Heineken 350ml' })
    await user.click(within(dialog).getAllByRole('button', { name: 'Fechar' })[index])

    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Heineken 350ml' })).not.toBeInTheDocument())
  })

  it('"Consultar outro" fecha a consulta e abre o leitor de novo', async () => {
    const user = userEvent.setup()
    getByBarcode.mockResolvedValue(product)
    renderWithAuth(<QuickProductLookup />, { role: 'VENDEDOR' })

    await openAndScan(user)
    await user.click(await screen.findByRole('button', { name: /Consultar outro/ }))

    expect(screen.getByRole('dialog', { name: 'Leitor de código de barras' })).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Heineken 350ml' })).not.toBeInTheDocument())
  })

  it('código inexistente: "Produto não encontrado." sem cadastro para o vendedor', async () => {
    const user = userEvent.setup()
    getByBarcode.mockRejectedValue(notFound())
    renderWithAuth(<QuickProductLookup />, { role: 'VENDEDOR' })

    await openAndScan(user, '7899999999995')

    expect(await screen.findByText('Produto não encontrado.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Cadastrar produto/ })).not.toBeInTheDocument()
  })

  it('código inexistente: quem pode cadastrar vai para o cadastro com o código preenchido', async () => {
    const user = userEvent.setup()
    getByBarcode.mockRejectedValue(notFound())
    renderWithAuth(<QuickProductLookup />, { role: 'ESTOQUISTA', route: '/' })

    await openAndScan(user, '7899999999995')
    await user.click(await screen.findByRole('button', { name: /Cadastrar produto/ }))

    expect(screen.getByTestId('location')).toHaveTextContent('/produtos?novo=1&codigo=7899999999995')
  })

  it('erro de conexão aparece no modal', async () => {
    const user = userEvent.setup()
    getByBarcode.mockRejectedValue(new AxiosError('Network Error', 'ERR_NETWORK'))
    renderWithAuth(<QuickProductLookup />, { role: 'VENDEDOR' })

    await openAndScan(user)

    expect(await screen.findByText(/Não foi possível conectar ao servidor/)).toBeInTheDocument()
  })
})
