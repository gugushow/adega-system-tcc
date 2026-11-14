import { getErrorMessage, isNotFound } from '../../api/http'
import { productsApi } from '../../api/products'
import type { Product } from '../../types/product'

export type ProductLookupResult =
  | { kind: 'found'; code: string; product: Product }
  | { kind: 'not-found'; code: string }
  | { kind: 'error'; code: string; message: string }

/**
 * Busca o produto de um código lido (câmera ou leitor USB). Quem decide se o código "bate" é a API
 * (GET /api/products/barcode/{código}): exato primeiro, depois a forma equivalente UPC-A ⇄ EAN-13.
 * Produtos inativos também voltam como "found": cada tela decide o que fazer com eles.
 */
export async function findProductByBarcode(code: string): Promise<ProductLookupResult> {
  try {
    return { kind: 'found', code, product: await productsApi.getByBarcode(code) }
  } catch (err) {
    return isNotFound(err) ? { kind: 'not-found', code } : { kind: 'error', code, message: getErrorMessage(err) }
  }
}

/** Cadastro de produto com o código já preenchido (a tela de Produtos só abre o formulário para quem pode cadastrar). */
export const newProductPath = (code: string) => `/produtos?novo=1&codigo=${encodeURIComponent(code)}`
