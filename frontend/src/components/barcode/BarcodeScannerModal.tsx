import { Button, Modal } from 'react-bootstrap'
import { BarcodeScanner } from './BarcodeScanner'

interface BarcodeScannerModalProps {
  show: boolean
  /** Recebe o código lido. O modal não fecha sozinho: quem chama decide (normalmente fecha). */
  onDetected: (code: string) => void
  onClose: () => void
}

/**
 * Modal com o leitor de código de barras. O BarcodeScanner só existe enquanto o modal está aberto,
 * então fechar (ou sair da tela) sempre desliga a câmera. Em celular ocupa a tela toda.
 */
export function BarcodeScannerModal({ show, onDetected, onClose }: BarcodeScannerModalProps) {
  return (
    <Modal
      show={show}
      onHide={onClose}
      centered
      fullscreen="sm-down"
      className="barcode-scanner-modal"
      backdropClassName="barcode-scanner-backdrop"
      aria-labelledby="barcode-scanner-title"
    >
      <Modal.Header closeButton closeLabel="Fechar">
        <Modal.Title as="h5" id="barcode-scanner-title">
          <i className="bi bi-upc-scan me-2" aria-hidden="true" />
          Escanear código de barras
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>{show && <BarcodeScanner onDetected={onDetected} />}</Modal.Body>
      <Modal.Footer>
        <Button variant="outline-secondary" onClick={onClose}>
          Cancelar
        </Button>
      </Modal.Footer>
    </Modal>
  )
}
