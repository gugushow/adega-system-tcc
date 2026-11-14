import type { BarcodeFormat, DetectedBarcode } from 'barcode-detector/ponyfill'

/** Formatos de embalagem de bebida/mercado. QR Code e outros ficam de fora (menos falso positivo). */
export const BARCODE_FORMATS: BarcodeFormat[] = ['ean_13', 'ean_8', 'upc_a', 'upc_e']

export interface BarcodeReader {
  /** Procura um código no quadro atual do vídeo. null quando não há nenhum. */
  detect(source: HTMLVideoElement): Promise<string | null>
}

interface Detector {
  detect(source: HTMLVideoElement): Promise<DetectedBarcode[]>
}

interface NativeDetectorClass {
  new (options: { formats: BarcodeFormat[] }): Detector
  getSupportedFormats(): Promise<readonly string[]>
}

const toReader = (detector: Detector): BarcodeReader => ({
  async detect(source) {
    const [first] = await detector.detect(source)
    return first?.rawValue.trim() || null
  },
})

/** BarcodeDetector do próprio navegador (ex.: Chrome no Android), se ele ler todos os formatos. */
async function loadNative(): Promise<BarcodeReader | null> {
  const Native = (globalThis as { BarcodeDetector?: NativeDetectorClass }).BarcodeDetector
  if (!Native) return null
  try {
    const supported = await Native.getSupportedFormats()
    if (!BARCODE_FORMATS.every((format) => supported.includes(format))) return null
    return toReader(new Native({ formats: BARCODE_FORMATS }))
  } catch {
    return null
  }
}

/**
 * Sem a API nativa (Safari, Firefox, Chrome no Windows): ZXing-C++ em WebAssembly, pela biblioteca
 * barcode-detector. Carregado só quando o leitor abre. O .wasm vai junto no build (sem CDN externo).
 */
async function loadZxing(): Promise<BarcodeReader> {
  const [{ BarcodeDetector, prepareZXingModule }, { default: wasmUrl }] = await Promise.all([
    import('barcode-detector/ponyfill'),
    import('zxing-wasm/reader/zxing_reader.wasm?url'),
  ])
  prepareZXingModule({
    overrides: {
      locateFile: (path: string, prefix: string) => (path.endsWith('.wasm') ? wasmUrl : prefix + path),
    },
  })
  return toReader(new BarcodeDetector({ formats: BARCODE_FORMATS }))
}

let readerPromise: Promise<BarcodeReader> | null = null

/** O mesmo leitor serve para todas as aberturas do scanner (o .wasm só é baixado uma vez). */
export function loadBarcodeReader(): Promise<BarcodeReader> {
  readerPromise ??= (async () => (await loadNative()) ?? (await loadZxing()))().catch((error: unknown) => {
    readerPromise = null // se falhar (ex.: sem internet), a próxima abertura tenta de novo
    throw error
  })
  return readerPromise
}
