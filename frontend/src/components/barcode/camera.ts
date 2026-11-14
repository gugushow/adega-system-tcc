/**
 * Acesso à câmera para o leitor de código de barras. Só é chamado depois de um clique do usuário
 * em "Escanear". Nada é gravado nem enviado: o vídeo fica só no <video> da tela e é desligado ao fechar.
 */

export type CameraErrorKind = 'insecure' | 'unsupported' | 'denied' | 'not-found' | 'busy' | 'unknown'

export class CameraError extends Error {
  readonly kind: CameraErrorKind

  constructor(kind: CameraErrorKind) {
    super(cameraErrorMessages[kind])
    this.name = 'CameraError'
    this.kind = kind
  }
}

const cameraErrorMessages: Record<CameraErrorKind, string> = {
  insecure:
    'A câmera só funciona em conexão segura (HTTPS) ou em localhost. Abra o sistema pelo endereço com HTTPS ou digite o código.',
  unsupported: 'Este navegador não permite usar a câmera. Atualize o navegador ou digite o código.',
  denied:
    'O acesso à câmera foi negado. Libere a câmera nas permissões do navegador (ícone ao lado do endereço) e tente de novo.',
  'not-found': 'Nenhuma câmera foi encontrada neste dispositivo.',
  busy: 'A câmera está sendo usada por outro aplicativo. Feche o outro aplicativo e tente de novo.',
  unknown: 'Não foi possível iniciar a câmera. Tente de novo ou digite o código.',
}

/** Traduz o erro do getUserMedia (DOMException) para um dos casos acima. */
function kindOf(error: unknown): CameraErrorKind {
  const name = error instanceof Error ? error.name : ''
  switch (name) {
    case 'NotAllowedError':
    case 'SecurityError':
      return 'denied'
    case 'NotFoundError':
    case 'OverconstrainedError':
      return 'not-found'
    case 'NotReadableError':
    case 'AbortError':
      return 'busy'
    default:
      return 'unknown'
  }
}

/** Abre a câmera, preferindo a traseira do celular. Sem áudio. */
export async function openCamera(): Promise<MediaStream> {
  if (!window.isSecureContext) {
    throw new CameraError('insecure')
  }
  if (!navigator.mediaDevices?.getUserMedia) {
    throw new CameraError('unsupported')
  }

  try {
    return await navigator.mediaDevices.getUserMedia({
      audio: false,
      // "ideal" e não "exact": no notebook (só câmera frontal) usa a que existir.
      video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
    })
  } catch (error) {
    throw new CameraError(kindOf(error))
  }
}

/** Desliga a câmera de verdade (a luz do dispositivo apaga). */
export function stopCamera(stream: MediaStream | null) {
  stream?.getTracks().forEach((track) => track.stop())
}

export function cameraErrorMessage(error: unknown): string {
  if (error instanceof CameraError) return error.message
  return 'Não foi possível iniciar o leitor de código de barras. Tente de novo ou digite o código.'
}
