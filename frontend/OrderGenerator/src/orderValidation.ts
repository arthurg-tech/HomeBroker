export const ASSETS = ['PETR4', 'VALE3', 'VIIA4'] as const

export type Asset = (typeof ASSETS)[number]
export type SideLabel = 'Compra' | 'Venda'

export interface OrderFormValues {
  ativo: Asset
  lado: SideLabel
  quantidade: string
  preco: string
}

export interface OrderRequest {
  ativo: Asset
  lado: 'C' | 'V'
  quantidade: number
  preco: number
}

export interface OrderResponse {
  sucesso: boolean
  exposicao_atual: number
  msg_erro: string
}

export interface OrderFormValidation {
  order?: OrderRequest
  errors: Partial<Record<keyof OrderFormValues, string>>
}

export function parsePriceToCents(value: string): bigint | null {
  const match = /^(\d+)(?:[.,](\d+))?$/.exec(value.trim())
  if (!match) return null

  const [, whole, fraction = ''] = match
  if (fraction.length > 2) return null

  const cents = BigInt(whole) * 100n + BigInt(fraction.slice(0, 2).padEnd(2, '0') || '00')
  return cents >= 1n && cents <= 99_999n ? cents : null
}

export function validateOrderForm(form: OrderFormValues): OrderFormValidation {
  const errors: OrderFormValidation['errors'] = {}
  const trimmedQuantity = form.quantidade.trim()
  const quantity = /^\d+$/.test(trimmedQuantity) ? Number(trimmedQuantity) : Number.NaN

  if (!trimmedQuantity) {
    errors.quantidade = 'Informe a quantidade.'
  } else if (!Number.isInteger(quantity)) {
    errors.quantidade = 'A quantidade deve ser um número inteiro.'
  } else if (quantity < 1 || quantity > 99_999) {
    errors.quantidade = 'A quantidade deve estar entre 1 e 99.999.'
  }

  const priceInCents = parsePriceToCents(form.preco)
  if (!form.preco.trim()) {
    errors.preco = 'Informe o preço por ação.'
  } else if (priceInCents === null) {
    errors.preco = 'Use até 2 casas decimais, de R$ 0,01 a R$ 999,99, sem separador de milhar e sem arredondar.'
  }

  if (Object.keys(errors).length > 0) return { errors }

  return {
    errors,
    order: {
      ativo: form.ativo,
      lado: form.lado === 'Compra' ? 'C' : 'V',
      quantidade: quantity,
      preco: Number(priceInCents) / 100,
    },
  }
}
