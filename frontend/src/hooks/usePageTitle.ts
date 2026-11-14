import { useEffect } from 'react'

/** Título da aba do navegador: "Produtos · Adega System". Ajuda quem trabalha com várias abas abertas. */
export function usePageTitle(title: string) {
  useEffect(() => {
    document.title = title ? `${title} · Adega System` : 'Adega System'
  }, [title])
}
