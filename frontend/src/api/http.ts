import axios from 'axios'
import { authStorage } from '../auth/authStorage'
import type { ApiError, ApiResponse } from '../types/api'

/**
 * Instância única do Axios usada por todo o frontend.
 * Em desenvolvimento, "/api" é encaminhado pelo Vite para o backend (ver vite.config.ts).
 */
export const http = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
})

// Antes de cada requisição: anexa o token do usuário logado no cabeçalho Authorization.
http.interceptors.request.use((config) => {
  const token = authStorage.getToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// Depois de cada resposta: 401 quer dizer que o token venceu ou deixou de valer
// (ex.: o ADMIN desativou o usuário). Limpa a sessão e volta para o login.
// O próprio login também responde 401 (senha errada); nesse caso a tela de login mostra o erro.
http.interceptors.response.use(
  (response) => response,
  (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 401 && !error.config?.url?.endsWith('/auth/login')) {
      authStorage.clear()
      // Guarda a tela atual para voltar a ela depois de entrar de novo.
      const here = window.location.pathname + window.location.search
      window.location.assign(`/login?sessao=expirada&de=${encodeURIComponent(here)}`)
    }
    return Promise.reject(error)
  },
)

/** Faz a requisição e devolve só o "data" da resposta padrão da API. */
export async function unwrap<T>(request: Promise<{ data: ApiResponse<T> }>): Promise<T> {
  const response = await request
  return response.data.data as T
}

/** Extrai uma mensagem amigável de qualquer erro (resposta da API, rede fora do ar, etc.). */
export function getErrorMessage(error: unknown): string {
  if (axios.isAxiosError<ApiResponse<unknown>>(error)) {
    if (error.response?.data?.message) {
      return error.response.data.message
    }
    if (!error.response) {
      return 'Não foi possível conectar ao servidor. Verifique a conexão e tente novamente.'
    }
  }
  return 'Ocorreu um erro inesperado. Tente novamente.'
}

/** A API respondeu 404 (ex.: nenhum produto com o código de barras lido). */
export const isNotFound = (error: unknown) => axios.isAxiosError(error) && error.response?.status === 404

/**
 * Erros de validação por campo ({ salePrice: "O preço de venda deve ser maior que zero." }).
 * Se um campo tiver mais de um erro, fica o primeiro (o mais básico, ex.: "muito curta").
 */
export function getFieldErrors(error: unknown): Record<string, string> {
  if (!axios.isAxiosError<ApiResponse<unknown>>(error)) {
    return {}
  }
  const errors: ApiError[] = error.response?.data?.errors ?? []
  const byField: Record<string, string> = {}
  for (const e of errors) {
    if (e.field && !(e.field in byField)) {
      byField[e.field] = e.message
    }
  }
  return byField
}
