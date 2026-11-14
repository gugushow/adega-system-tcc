import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { fakeScan } from '../../test/fakeScannerModal'
import { makeProduct } from '../../test/render'
import { ProductFormModal } from './ProductFormModal'

vi.mock('../../components/barcode/BarcodeScannerModal', async () => ({
  BarcodeScannerModal: (await import('../../test/fakeScannerModal')).FakeScannerModal,
}))

const categories = [{ id: 1, name: 'Cerveja' }] as never[]

function renderForm(props: Partial<Parameters<typeof ProductFormModal>[0]> = {}) {
  return render(
    <ProductFormModal
      product={null}
      categories={categories}
      minimumMargin={null}
      onClose={vi.fn()}
      onSaved={vi.fn()}
      {...props}
    />,
  )
}

describe('ProductFormModal: código de barras', () => {
  it('o código lido pela câmera preenche o campo, que continua editável', async () => {
    const user = userEvent.setup()
    fakeScan.code = '7891234567895'
    renderForm()

    await user.click(screen.getByRole('button', { name: 'Escanear código de barras' }))
    await user.click(screen.getByRole('button', { name: 'Simular leitura' }))

    const field = screen.getByLabelText('Código de barras')
    expect(field).toHaveValue('7891234567895')
    expect(screen.queryByRole('dialog', { name: 'Leitor de código de barras' })).not.toBeInTheDocument()

    await user.clear(field)
    await user.type(field, '7890000000000')
    expect(field).toHaveValue('7890000000000')
  })

  it('na edição, a leitura troca o código atual do produto', async () => {
    const user = userEvent.setup()
    fakeScan.code = '036000291452'
    renderForm({ product: makeProduct({ barcode: '7891234567895' }) })

    expect(screen.getByLabelText('Código de barras')).toHaveValue('7891234567895')
    await user.click(screen.getByRole('button', { name: 'Escanear código de barras' }))
    await user.click(screen.getByRole('button', { name: 'Simular leitura' }))

    expect(screen.getByLabelText('Código de barras')).toHaveValue('036000291452')
  })

  it('cancelar o leitor não muda o campo', async () => {
    const user = userEvent.setup()
    renderForm({ initialBarcode: '123' })

    await user.click(screen.getByRole('button', { name: 'Escanear código de barras' }))
    await user.click(screen.getByRole('button', { name: 'Fechar leitor' }))

    expect(screen.getByLabelText('Código de barras')).toHaveValue('123')
  })
})
