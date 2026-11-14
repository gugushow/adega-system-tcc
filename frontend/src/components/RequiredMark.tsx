/**
 * Asterisco de campo obrigatório. Na tela aparece só o "*"; o leitor de tela lê "(obrigatório)".
 */
export function RequiredMark() {
  return (
    <>
      <span className="required-mark" aria-hidden="true">
        *
      </span>
      <span className="visually-hidden"> (obrigatório)</span>
    </>
  )
}

/** Texto de rótulo que termina com " *" (ex.: "Nome *") vira o texto com o asterisco padronizado. */
export function FieldLabel({ text }: { text: string }) {
  if (!text.endsWith(' *')) return <>{text}</>
  return (
    <>
      {text.slice(0, -2)}
      <RequiredMark />
    </>
  )
}
