import type { OrderRequest, OrderResponse } from './orderValidation'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5177'

const expectedStatuses = new Set([200, 400, 422])

function isOrderResponse(value: unknown): value is OrderResponse {
  if (typeof value !== 'object' || value === null) return false

  const response = value as Partial<OrderResponse>
  return typeof response.sucesso === 'boolean'
    && typeof response.exposicao_atual === 'number'
    && Number.isFinite(response.exposicao_atual)
    && typeof response.msg_erro === 'string'
}

export async function sendOrder(order: OrderRequest): Promise<{ status: number; body: OrderResponse }> {
  const response = await fetch(`${API_BASE_URL}/api/ordens`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(order),
  })

  if (!expectedStatuses.has(response.status)) {
    throw new Error(`Status HTTP inesperado: ${response.status}`)
  }

  const body: unknown = await response.json()
  if (!isOrderResponse(body)) {
    throw new Error('A API retornou um JSON fora do contrato esperado.')
  }

  const matchesStatus = response.status === 200
    ? body.sucesso && body.msg_erro === ''
    : !body.sucesso && body.msg_erro.trim().length > 0
  if (!matchesStatus) {
    throw new Error('A resposta da API não corresponde ao status HTTP.')
  }

  return { status: response.status, body }
}
