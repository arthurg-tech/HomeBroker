import type { OrderRequest, OrderResponse } from './orderValidation'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5177'

export async function sendOrder(order: OrderRequest): Promise<{ status: number; body: OrderResponse }> {
  const response = await fetch(`${API_BASE_URL}/api/ordens`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(order),
  })

  return { status: response.status, body: await response.json() as OrderResponse }
}
