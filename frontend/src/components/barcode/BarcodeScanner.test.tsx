import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { BarcodeScanner } from './BarcodeScanner'
import { BarcodeScannerModal } from './BarcodeScannerModal'

// Nenhum teste usa câmera de verdade: getUserMedia devolve um "stream" falso e o leitor é simulado.
const detect = vi.fn<(source: HTMLVideoElement) => Promise<string | null>>()
vi.mock('./barcodeReader', () => ({
  loadBarcodeReader: () => Promise.resolve({ detect }),
}))

function fakeStream() {
  const track = { stop: vi.fn() }
  return { track, stream: { getTracks: () => [track] } as unknown as MediaStream }
}

let getUserMedia: ReturnType<typeof vi.fn>

beforeEach(() => {
  detect.mockReset()
  getUserMedia = vi.fn()
  Object.defineProperty(window, 'isSecureContext', { value: true, configurable: true })
  Object.defineProperty(navigator, 'mediaDevices', { value: { getUserMedia }, configurable: true })
  // O jsdom não toca vídeo: play() resolve na hora e o vídeo já "tem imagem".
  vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue(undefined)
  Object.defineProperty(HTMLMediaElement.prototype, 'readyState', { get: () => 4, configurable: true })
})

describe('BarcodeScanner', () => {
  it('pede a câmera traseira, devolve o código lido uma única vez e desliga a câmera', async () => {
    const { track, stream } = fakeStream()
    getUserMedia.mockResolvedValue(stream)
    detect.mockResolvedValueOnce(null).mockResolvedValue('7891234567895')
    const onDetected = vi.fn()

    render(<BarcodeScanner onDetected={onDetected} />)

    await waitFor(() => expect(onDetected).toHaveBeenCalledWith('7891234567895'))
    expect(getUserMedia).toHaveBeenCalledWith(
      expect.objectContaining({ audio: false, video: expect.objectContaining({ facingMode: { ideal: 'environment' } }) }),
    )
    expect(track.stop).toHaveBeenCalled()
    // O leitor continuaria achando o mesmo código, mas a leitura já terminou.
    await new Promise((resolve) => setTimeout(resolve, 400))
    expect(onDetected).toHaveBeenCalledTimes(1)
  })

  it('desliga a câmera quando o componente sai da tela', async () => {
    const { track, stream } = fakeStream()
    getUserMedia.mockResolvedValue(stream)
    detect.mockResolvedValue(null)

    const { unmount } = render(<BarcodeScanner onDetected={vi.fn()} />)
    await screen.findByText('Aponte a câmera para o código de barras.')

    unmount()

    expect(track.stop).toHaveBeenCalled()
  })

  it('mostra mensagem amigável quando a permissão da câmera é negada', async () => {
    getUserMedia.mockRejectedValue(Object.assign(new Error('denied'), { name: 'NotAllowedError' }))
    const onDetected = vi.fn()

    render(<BarcodeScanner onDetected={onDetected} />)

    expect(await screen.findByRole('alert')).toHaveTextContent('O acesso à câmera foi negado.')
    expect(screen.getByRole('button', { name: /Tentar de novo/ })).toBeInTheDocument()
    expect(onDetected).not.toHaveBeenCalled()
  })

  it('mostra mensagem quando não há câmera', async () => {
    getUserMedia.mockRejectedValue(Object.assign(new Error('none'), { name: 'NotFoundError' }))

    render(<BarcodeScanner onDetected={vi.fn()} />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Nenhuma câmera foi encontrada')
  })

  it('explica que precisa de HTTPS e nem pede a câmera fora de conexão segura', async () => {
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true })

    render(<BarcodeScanner onDetected={vi.fn()} />)

    expect(await screen.findByRole('alert')).toHaveTextContent('HTTPS')
    expect(getUserMedia).not.toHaveBeenCalled()
  })

  it('mostra mensagem quando o navegador não tem acesso à câmera', async () => {
    Object.defineProperty(navigator, 'mediaDevices', { value: undefined, configurable: true })

    render(<BarcodeScanner onDetected={vi.fn()} />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Este navegador não permite usar a câmera')
  })
})

describe('BarcodeScannerModal', () => {
  function Harness() {
    const [show, setShow] = useState(false)
    return (
      <>
        <button onClick={() => setShow(true)}>Abrir</button>
        <BarcodeScannerModal show={show} onDetected={vi.fn()} onClose={() => setShow(false)} />
      </>
    )
  }

  it('só liga a câmera depois do clique e desliga ao cancelar', async () => {
    const user = userEvent.setup()
    const { track, stream } = fakeStream()
    getUserMedia.mockResolvedValue(stream)
    detect.mockResolvedValue(null)

    render(<Harness />)
    expect(getUserMedia).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Abrir' }))
    await screen.findByText('Aponte a câmera para o código de barras.')
    expect(getUserMedia).toHaveBeenCalledTimes(1)

    await user.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(track.stop).toHaveBeenCalled()
  })
})
