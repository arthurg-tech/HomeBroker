import { describe, expect, it } from 'vitest'
import { parsePriceToCents, validateOrderForm, type OrderFormValues } from './orderValidation'

describe('price validation', () => {
  it.each([
    ['0.01', 1n],
    ['0,01', 1n],
    ['54.87', 5_487n],
    ['54,87', 5_487n],
    ['10.000', 1_000n],
    ['10,000', 1_000n],
    ['999.99', 99_999n],
  ])('converts %s to exact cents', (text, expected) => {
    expect(parsePriceToCents(text)).toBe(expected)
  })

  it.each(['', '0', '1000', '10.001', '10,001', '1.234,56', '1,234.56', '-1.00', '1e2', '.50', '10,'])(
    'rejects invalid decimal input %s without rounding', (text) => {
      expect(parsePriceToCents(text)).toBeNull()
    },
  )
})

describe('order form validation', () => {
  const form: OrderFormValues = { ativo: 'PETR4', lado: 'Compra', quantidade: '1', preco: '1.00' }

  it('keeps missing quantity and price invalid instead of converting blanks to zero', () => {
    const result = validateOrderForm({ ...form, quantidade: '', preco: '' })

    expect(result.order).toBeUndefined()
    expect(result.errors.quantidade).toMatch(/Informe/)
    expect(result.errors.preco).toMatch(/Informe/)
  })

  it('maps the visible sale label to the API side code and builds numeric values', () => {
    const result = validateOrderForm({ ...form, lado: 'Venda', quantidade: '584', preco: '54,87' })

    expect(result.errors).toEqual({})
    expect(result.order).toEqual({ ativo: 'PETR4', lado: 'V', quantidade: 584, preco: 54.87 })
    expect(typeof result.order?.preco).toBe('number')
  })

  it.each(['1.5', '0', '100000', '1e2'])('rejects quantity %s before building a request', (quantity) => {
    expect(validateOrderForm({ ...form, quantidade: quantity }).order).toBeUndefined()
  })
})
