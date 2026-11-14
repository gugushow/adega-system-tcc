import { useState } from 'react'
import { Button } from 'react-bootstrap'
import { useNavigate } from 'react-router'
import { BarcodeScannerModal } from './BarcodeScannerModal'
import { findProductByBarcode, newProductPath, type ProductLookupResult } from './productLookup'
import { ProductQuickViewModal } from './ProductQuickViewModal'

/**
 * Botão "Consultar produto" da tela inicial: lê o código pela câmera (o mesmo leitor do cadastro e do PDV)
 * e mostra o produto num modal. Serve para os três perfis; cada um vê só o que já pode ver no sistema.
 */
export function QuickProductLookup() {
  const navigate = useNavigate()
  const [scanning, setScanning] = useState(false)
  const [searching, setSearching] = useState(false)
  const [result, setResult] = useState<ProductLookupResult | null>(null)

  async function handleScanned(code: string) {
    setScanning(false)
    setSearching(true)
    setResult(await findProductByBarcode(code))
    setSearching(false)
  }

  function scanAgain() {
    setResult(null)
    setScanning(true)
  }

  return (
    <>
      <Button onClick={() => setScanning(true)} disabled={searching}>
        <i className="bi bi-upc-scan me-2" aria-hidden="true" />
        {searching ? 'Buscando...' : 'Consultar produto'}
      </Button>

      <BarcodeScannerModal show={scanning} onDetected={handleScanned} onClose={() => setScanning(false)} />

      {result && (
        <ProductQuickViewModal
          result={result}
          onClose={() => setResult(null)}
          onScanAgain={scanAgain}
          onCreateProduct={(code) => navigate(newProductPath(code))}
        />
      )}
    </>
  )
}
