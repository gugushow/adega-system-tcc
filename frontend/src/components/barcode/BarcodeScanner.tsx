import { useEffect, useRef, useState } from 'react'
import { Button, Spinner } from 'react-bootstrap'
import { loadBarcodeReader } from './barcodeReader'
import { cameraErrorMessage, openCamera, stopCamera } from './camera'
import './BarcodeScanner.css'

/** Intervalo entre as tentativas de leitura. Rápido o suficiente sem esquentar o celular. */
const SCAN_INTERVAL_MS = 150

type Status = 'starting' | 'scanning' | 'error'

interface BarcodeScannerProps {
  /** Chamado uma única vez, com o código lido. A câmera já está desligada nesse momento. */
  onDetected: (code: string) => void
}

/**
 * Vídeo da câmera que procura um código de barras (EAN-13, EAN-8, UPC-A, UPC-E).
 * A câmera liga quando o componente aparece e desliga quando ele lê um código ou sai da tela
 * (por isso só é montado depois do clique em "Escanear"). Nenhuma imagem é guardada ou enviada.
 */
export function BarcodeScanner({ onDetected }: BarcodeScannerProps) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const [status, setStatus] = useState<Status>('starting')
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)

  // A tela costuma passar uma função nova a cada renderização: o ref evita reiniciar a câmera por isso.
  const onDetectedRef = useRef(onDetected)
  useEffect(() => {
    onDetectedRef.current = onDetected
  })

  useEffect(() => {
    const video = videoRef.current
    let stopped = false
    let stream: MediaStream | null = null
    let timer: ReturnType<typeof setTimeout> | undefined

    function stop() {
      stopped = true
      clearTimeout(timer)
      stopCamera(stream)
      stream = null
      if (video) video.srcObject = null
    }

    async function start() {
      try {
        const opened = await openCamera()
        if (stopped) {
          stopCamera(opened) // a tela fechou enquanto o navegador pedia permissão
          return
        }
        stream = opened
        const reader = await loadBarcodeReader()
        if (stopped || !video) return

        video.srcObject = opened
        await video.play()
        if (stopped) return
        setStatus('scanning')

        const scan = async () => {
          if (stopped) return
          try {
            // Só tenta ler quando já existe um quadro de vídeo (readyState 2 = HAVE_CURRENT_DATA).
            const code = video.readyState >= 2 ? await reader.detect(video) : null
            if (code && !stopped) {
              stop() // desliga antes de avisar: a mesma leitura nunca chega duas vezes
              navigator.vibrate?.(60)
              onDetectedRef.current(code)
              return
            }
          } catch {
            // Um quadro ruim não é erro: tenta de novo no próximo.
          }
          if (!stopped) timer = setTimeout(scan, SCAN_INTERVAL_MS)
        }
        void scan()
      } catch (err) {
        if (stopped) return
        stop()
        setError(cameraErrorMessage(err))
        setStatus('error')
      }
    }

    void start()
    return stop
  }, [attempt])

  function retry() {
    setError(null)
    setStatus('starting')
    setAttempt((n) => n + 1)
  }

  return (
    <div className="barcode-scanner">
      <div className="barcode-scanner-viewport" hidden={status === 'error'}>
        {/* muted + playsInline: sem isso o iPhone não mostra o vídeo dentro da página. */}
        <video ref={videoRef} className="barcode-scanner-video" muted playsInline aria-label="Imagem da câmera" />
        {status === 'scanning' && <div className="barcode-scanner-guide" aria-hidden="true" />}
        {status === 'starting' && (
          <div className="barcode-scanner-overlay" role="status">
            <Spinner animation="border" size="sm" aria-hidden="true" />
            <span>Iniciando a câmera...</span>
          </div>
        )}
      </div>

      {status === 'error' ? (
        <div className="barcode-scanner-error" role="alert">
          <i className="bi bi-camera-video-off fs-2" aria-hidden="true" />
          <p className="mb-0">{error}</p>
          <Button variant="outline-secondary" size="sm" onClick={retry}>
            <i className="bi bi-arrow-clockwise me-1" aria-hidden="true" />
            Tentar de novo
          </Button>
        </div>
      ) : (
        <p className="text-secondary text-center small mb-0 mt-2" aria-live="polite">
          {status === 'scanning'
            ? 'Aponte a câmera para o código de barras.'
            : 'Se o navegador perguntar, permita o uso da câmera.'}
        </p>
      )}
    </div>
  )
}
