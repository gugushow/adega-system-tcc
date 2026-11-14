/**
 * Substituto do BarcodeScannerModal nos testes das telas (vi.mock). Em vez da câmera, um botão
 * "Simular leitura" entrega o código, como se a câmera tivesse lido.
 */
export function FakeScannerModal({
  show,
  onDetected,
  onClose,
}: {
  show: boolean
  onDetected: (code: string) => void
  onClose: () => void
}) {
  if (!show) return null
  return (
    <div role="dialog" aria-label="Leitor de código de barras">
      <button onClick={() => onDetected(fakeScan.code)}>Simular leitura</button>
      <button onClick={onClose}>Fechar leitor</button>
    </div>
  )
}

/** Código que a próxima "leitura" devolve. */
export const fakeScan = { code: '7891234567895' }
