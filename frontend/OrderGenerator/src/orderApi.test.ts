import { afterEach, describe, expect, it, vi } from 'vitest'
import { sendOrder } from './orderApi'
import type { OrderRequest } from './orderValidation'

afterEach(() => vi.unstubAllGlobals())

const order: OrderRequest = {
  ativo: 'PETR4',
  lado: 'C',
  quantidade: 2,
  preco: 54.87,
}

function apiResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('sendOrder', () => {
  it('reads the API contract from a business rejection response', async () => {
    const body = {
      sucesso: false,
      exposicao_atual: 1_000_000,
      msg_erro: 'Limite de exposição excedido.',
    }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(apiResponse(422, body)))

    await expect(sendOrder(order)).resolves.toEqual({ status: 422, body })
  })

  it('rejects a valid JSON body that does not match the API contract', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(apiResponse(200, {
      sucesso: true,
      exposicao_atual: '100',
      msg_erro: '',
    })))

    await expect(sendOrder(order)).rejects.toThrow('JSON fora do contrato esperado')
  })

  it('rejects an unexpected HTTP status even when its body looks valid', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(apiResponse(500, {
      sucesso: false,
      exposicao_atual: 0,
      msg_erro: 'Erro interno.',
    })))

    await expect(sendOrder(order)).rejects.toThrow('Status HTTP inesperado: 500')
  })

  it('rejects a status and body that contradict each other', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(apiResponse(422, {
      sucesso: true,
      exposicao_atual: 108.74,
      msg_erro: '',
    })))

    await expect(sendOrder(order)).rejects.toThrow('não corresponde ao status HTTP')
  })
})
