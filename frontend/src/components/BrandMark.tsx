/**
 * Marca do sistema: uma taça com vinho desenhada em SVG (o Bootstrap Icons não tem taça de vinho).
 * O favicon (public/favicon.svg) usa o mesmo desenho, branco sobre o bordô do tema.
 */
export function BrandMark({ size = 20 }: { size?: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M7.5 3h9l-.6 6a3.9 3.9 0 0 1-7.8 0L7.5 3z" />
      {/* O vinho: preenche o fundo da taça (deixa o ícone reconhecível em tamanho pequeno). */}
      <path d="M7.95 7.5h8.1L15.9 9a3.9 3.9 0 0 1-7.8 0z" fill="currentColor" />
      <path d="M12 13v7.5" />
      <path d="M8.5 21h7" />
    </svg>
  )
}
