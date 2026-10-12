import { cleanup, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function successfulResponse(exposure: number) {
  return new Response(JSON.stringify({ sucesso: true, exposicao_atual: exposure, msg_erro: '' }), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('OrderGenerator form', () => {
  it('shows the required, labelled fields and their limits', () => {
    render(<App />)

    expect(screen.getByRole('combobox', { name: 'Ativo' })).toHaveValue('PETR4')
    expect(within(screen.getByRole('combobox', { name: 'Ativo' })).getAllByRole('option').map((option) => option.textContent))
      .toEqual(['PETR4', 'VALE3', 'VIIA4'])
    expect(screen.getByRole('combobox', { name: 'Lado' })).toHaveValue('Compra')
    expect(within(screen.getByRole('combobox', { name: 'Lado' })).getAllByRole('option').map((option) => option.textContent))
      .toEqual(['Compra', 'Venda'])

    const quantity = screen.getByRole('textbox', { name: 'Quantidade' })
    expect(quantity).toBeRequired()
    expect(quantity).toHaveAttribute('inputmode', 'numeric')
    expect(quantity).toHaveValue('')
    expect(quantity).toHaveAccessibleDescription('De 1 a 99.999 ações')

    const price = screen.getByRole('textbox', { name: 'Preço por ação' })
    expect(price).toBeRequired()
    expect(price).toHaveAttribute('inputmode', 'numeric')
    expect(price).toHaveValue('0,00')
    expect(price).toHaveAccessibleDescription(/De R\$ 0,01 a R\$ 999,99/)
  })

  it('shows errors for missing quantity and zero price without posting', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    expect(await screen.findByText('Informe a quantidade.')).toBeInTheDocument()
    expect(screen.getByText(/de R\$ 0,01 a R\$ 999,99/)).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: 'Quantidade' })).toHaveValue('')
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('lets a keyboard user reach every form field and submit', async () => {
    const user = userEvent.setup()
    render(<App />)
    const focusOrder = [
      screen.getByRole('link', { name: 'Home Broker, início' }),
      screen.getByRole('combobox', { name: 'Ativo' }),
      screen.getByRole('combobox', { name: 'Lado' }),
      screen.getByRole('textbox', { name: 'Quantidade' }),
      screen.getByRole('textbox', { name: 'Preço por ação' }),
      screen.getByRole('button', { name: 'Enviar ordem' }),
    ]

    for (const element of focusOrder) {
      await user.tab()
      expect(element).toHaveFocus()
    }

    await user.keyboard('{Enter}')
    expect(await screen.findByText('Informe a quantidade.')).toBeInTheDocument()
    expect(screen.getByText(/de R\$ 0,01 a R\$ 999,99/)).toBeInTheDocument()
  })

  it.each([
    ['Compra', 'C', '5487', 32_044.08],
    ['Compra', 'C', '54.87', 32_044.08],
    ['Compra', 'C', '54,87', 32_044.08],
    ['Venda', 'V', '54.87', -32_044.08],
    ['Venda', 'V', '54,87', -32_044.08],
  ])('sends %s (%s) with numeric fields from price %s', async (side, code, price, exposure) => {
    const fetchMock = vi.fn().mockResolvedValue(successfulResponse(exposure))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.selectOptions(screen.getByRole('combobox', { name: 'Ativo' }), 'VALE3')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Lado' }), side)
    await user.type(screen.getByRole('textbox', { name: 'Quantidade' }), '584')
    await user.type(screen.getByRole('textbox', { name: 'Preço por ação' }), price)
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))
    const [url, request] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toMatch(/\/api\/ordens$/)
    expect(request.method).toBe('POST')
    expect(request.headers).toEqual({ 'Content-Type': 'application/json' })
    const payload = JSON.parse(String(request.body)) as Record<string, unknown>
    expect(payload).toEqual({ ativo: 'VALE3', lado: code, quantidade: 584, preco: 54.87 })
    expect(typeof payload.quantidade).toBe('number')
    expect(typeof payload.preco).toBe('number')
    expect(await screen.findByText('Resposta da API')).toBeInTheDocument()
    expect(screen.getByText(/32\.044,08/)).toBeInTheDocument()
    expect(screen.getByText('HTTP 200')).toBeInTheDocument()
  })

  it('formats thousands while typing and stops at five quantity digits', async () => {
    const user = userEvent.setup()
    render(<App />)
    const quantity = screen.getByRole('textbox', { name: 'Quantidade' })

    await user.type(quantity, '1000')
    expect(quantity).toHaveValue('1.000')
    await user.clear(quantity)
    await user.type(quantity, '99999')
    expect(quantity).toHaveValue('99.999')
    await user.type(quantity, '123456789')
    expect(quantity).toHaveValue('99.999')
  })

  it('fills price from cents and stops at R$ 999,99', async () => {
    const user = userEvent.setup()
    render(<App />)
    const price = screen.getByRole('textbox', { name: 'Preço por ação' })

    expect(price).toHaveValue('0,00')
    await user.type(price, '1')
    expect(price).toHaveValue('0,01')
    await user.type(price, '2')
    expect(price).toHaveValue('0,12')
    await user.type(price, '3')
    expect(price).toHaveValue('1,23')
    await user.clear(price)
    expect(price).toHaveValue('0,00')
    await user.type(price, '99999')
    expect(price).toHaveValue('999,99')
    await user.type(price, '123456789')
    expect(price).toHaveValue('999,99')
  })

  it('shifts cents back when deleting and supports replacing the selected price', async () => {
    const user = userEvent.setup()
    render(<App />)
    const price = screen.getByRole('textbox', { name: 'Preço por ação' })

    await user.type(price, '5487')
    expect(price).toHaveValue('54,87')
    await user.keyboard('{Backspace}')
    expect(price).toHaveValue('5,48')
    await user.keyboard('{Backspace}{Backspace}{Backspace}{Backspace}')
    expect(price).toHaveValue('0,00')
    await user.type(price, '12345')
    await user.keyboard('{Control>}a{/Control}99')
    expect(price).toHaveValue('0,99')
  })

  it('keeps the quantity caret beside the edited digits as separators change', async () => {
    const user = userEvent.setup()
    render(<App />)
    const quantity = screen.getByRole('textbox', { name: 'Quantidade' }) as HTMLInputElement

    await user.type(quantity, '1234')
    expect(quantity).toHaveValue('1.234')
    quantity.setSelectionRange(3, 3)
    await user.keyboard('5')
    expect(quantity).toHaveValue('12.534')
    expect(quantity.selectionStart).toBe(4)
    await user.keyboard('{Backspace}')
    expect(quantity).toHaveValue('1.234')
    expect(quantity.selectionStart).toBe(3)
    await user.keyboard('{End}{Backspace}')
    expect(quantity).toHaveValue('123')
  })

  it('accepts pasted formatted amounts and rejects overflowing pastes without truncation', async () => {
    const user = userEvent.setup()
    render(<App />)
    const quantity = screen.getByRole('textbox', { name: 'Quantidade' })
    const price = screen.getByRole('textbox', { name: 'Preço por ação' })

    await user.click(quantity)
    await user.paste('99.999')
    expect(quantity).toHaveValue('99.999')
    await user.keyboard('{Control>}a{/Control}')
    await user.paste('100000')
    expect(quantity).toHaveValue('99.999')
    await user.click(price)
    await user.keyboard('{Control>}a{/Control}')
    await user.paste('999,99')
    expect(price).toHaveValue('999,99')
    await user.keyboard('{Control>}a{/Control}')
    await user.paste('1000,00')
    expect(price).toHaveValue('999,99')
  })

  it('rejects letters and signs instead of sending a different numeric value', async () => {
    const user = userEvent.setup()
    render(<App />)
    const quantity = screen.getByRole('textbox', { name: 'Quantidade' })
    const price = screen.getByRole('textbox', { name: 'Preço por ação' })

    await user.type(quantity, '-e+')
    expect(quantity).toHaveValue('')
    await user.paste('1e3')
    expect(quantity).toHaveValue('')
    await user.click(price)
    await user.paste('-54,87')
    expect(price).toHaveValue('0,00')
  })

  it('preserves decimal amounts when pasting and rejects malformed separators', async () => {
    const user = userEvent.setup()
    render(<App />)
    const quantity = screen.getByRole('textbox', { name: 'Quantidade' })
    const price = screen.getByRole('textbox', { name: 'Preço por ação' })

    await user.type(quantity, '1')
    await user.paste('1.5')
    expect(quantity).toHaveValue('1')
    await user.click(price)
    await user.paste('54,8')
    expect(price).toHaveValue('54,80')
    await user.paste('R$ 54,87')
    expect(price).toHaveValue('54,87')
    for (const invalid of ['10,001', '1.234,56', '1,234.56']) {
      await user.paste(invalid)
      expect(price).toHaveValue('54,87')
    }
  })

  it('keeps price entry at the cents end after moving the caret', async () => {
    const user = userEvent.setup()
    render(<App />)
    const price = screen.getByRole('textbox', { name: 'Preço por ação' })

    await user.type(price, '12')
    await user.keyboard('{Home}3')
    expect(price).toHaveValue('1,23')
    await user.keyboard('{ArrowLeft}{Backspace}')
    expect(price).toHaveValue('0,12')
  })

  it('blocks zero quantities and prices', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.type(screen.getByRole('textbox', { name: 'Quantidade' }), '0')
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))
    expect(await screen.findByText(/entre 1 e 99\.999/)).toBeInTheDocument()
    expect(screen.getByText(/de R\$ 0,01 a R\$ 999,99/)).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('sends formatted thousands and the maximum price as numeric API values', async () => {
    const fetchMock = vi.fn().mockResolvedValue(successfulResponse(999_990))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.type(screen.getByRole('textbox', { name: 'Quantidade' }), '1000')
    await user.type(screen.getByRole('textbox', { name: 'Preço por ação' }), '99999')
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))
    const request = fetchMock.mock.calls[0][1] as RequestInit
    expect(JSON.parse(String(request.body))).toEqual({ ativo: 'PETR4', lado: 'C', quantidade: 1000, preco: 999.99 })
  })

  it.each([
    [400, 54.87, 'Quantidade deve ser um inteiro entre 1 e 99.999.'],
    [422, 990_000, 'Limite de exposição excedido.'],
  ])('presents HTTP %s, the error message and the response JSON', async (status, exposure, message) => {
    const rejected = { sucesso: false, exposicao_atual: exposure, msg_erro: message }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(rejected), {
      status,
      headers: { 'Content-Type': 'application/json' },
    })))
    const user = userEvent.setup()
    render(<App />)

    await user.type(screen.getByRole('textbox', { name: 'Quantidade' }), '1')
    await user.type(screen.getByRole('textbox', { name: 'Preço por ação' }), '10,00')
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    expect(await screen.findByText(`HTTP ${status}`)).toBeInTheDocument()
    expect(screen.getByText(message)).toBeInTheDocument()
    await user.click(screen.getByText('Ver resposta JSON'))
    expect(screen.getByText((_content, element) => element?.tagName === 'PRE'))
      .toHaveTextContent(`"exposicao_atual": ${exposure}`)
    expect(screen.getByRole('button', { name: 'Enviar ordem' })).toBeEnabled()
  })

  it.each([null, '30'])('presents rate limiting with Retry-After %s and allows a manual retry', async (retryAfter) => {
    const message = 'Limite de requisições excedido. Aguarde antes de enviar outra ordem.'
    const rejected = { sucesso: false, exposicao_atual: 0, msg_erro: message }
    const response = new Response(JSON.stringify(rejected), {
      status: 429,
      headers: { 'Content-Type': 'application/json' },
    })
    if (retryAfter !== null) response.headers.set('Retry-After', retryAfter)
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response)
      .mockResolvedValueOnce(successfulResponse(10))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.type(screen.getByRole('textbox', { name: 'Quantidade' }), '1')
    await user.type(screen.getByRole('textbox', { name: 'Preço por ação' }), '10,00')
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    expect(await screen.findByRole('heading', { name: 'Limite de requisições atingido' })).toBeInTheDocument()
    expect(screen.getByText('HTTP 429')).toBeInTheDocument()
    expect(screen.getByText(message)).toBeInTheDocument()
    expect(screen.getByText(/A ordem não foi processada\. A exposição dos ativos foi preservada\./)).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Falha de comunicação' })).not.toBeInTheDocument()
    expect(screen.queryByText('Exposição atual')).not.toBeInTheDocument()
    expect(screen.queryByText(/^R\$\s*0,00$/)).not.toBeInTheDocument()
    if (retryAfter !== null) {
      expect(screen.getByText(/Espera estimada pela API: 30 segundos\./)).toBeInTheDocument()
    } else {
      expect(screen.queryByText(/Espera estimada pela API:/)).not.toBeInTheDocument()
    }

    await user.click(screen.getByText('Ver resposta JSON'))
    expect(screen.getByText((_content, element) => element?.tagName === 'PRE'))
      .toHaveTextContent('"exposicao_atual": 0')
    expect(screen.getByRole('button', { name: 'Enviar ordem' })).toBeEnabled()
    expect(fetchMock).toHaveBeenCalledTimes(1)

    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    expect(await screen.findByText('Ordem aceita com sucesso.')).toBeInTheDocument()
    expect(screen.getByText('HTTP 200')).toBeInTheDocument()
    expect(screen.getByText('Exposição atual')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Limite de requisições atingido' })).not.toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('shows the pending state and disables submission until the API responds', async () => {
    let resolveResponse!: (response: Response) => void
    const fetchMock = vi.fn(() => new Promise<Response>((resolve) => {
      resolveResponse = resolve
    }))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.type(screen.getByRole('textbox', { name: 'Quantidade' }), '2')
    await user.type(screen.getByRole('textbox', { name: 'Preço por ação' }), '54,87')
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    const pendingButton = await screen.findByRole('button', { name: 'Enviando…' })
    expect(pendingButton).toBeDisabled()
    await user.click(pendingButton)
    expect(fetchMock).toHaveBeenCalledTimes(1)

    resolveResponse(successfulResponse(109.74))

    expect(await screen.findByText('Ordem aceita com sucesso.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Enviar ordem' })).toBeEnabled()
  })

  it('reports a network failure without claiming rejection or showing an exposure', async () => {
    const fetchMock = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.type(screen.getByRole('textbox', { name: 'Quantidade' }), '2')
    await user.type(screen.getByRole('textbox', { name: 'Preço por ação' }), '54,87')
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    expect(await screen.findByRole('heading', { name: 'Falha de comunicação' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível confirmar a resposta')
    expect(screen.queryByText('Resposta da API')).not.toBeInTheDocument()
    expect(screen.queryByText('Exposição atual')).not.toBeInTheDocument()
    expect(screen.queryByText(/rejeitada pelo servidor/i)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Enviar ordem' })).toBeEnabled()
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('treats an unexpected HTTP response as unconfirmed and hides its body', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      sucesso: false,
      exposicao_atual: 0,
      msg_erro: 'Falha interna.',
    }), {
      status: 500,
      headers: { 'Content-Type': 'application/json' },
    }))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.type(screen.getByRole('textbox', { name: 'Quantidade' }), '2')
    await user.type(screen.getByRole('textbox', { name: 'Preço por ação' }), '54,87')
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    expect(await screen.findByRole('heading', { name: 'Falha de comunicação' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível confirmar a resposta')
    expect(screen.queryByText('Resposta da API')).not.toBeInTheDocument()
    expect(screen.queryByText('Exposição atual')).not.toBeInTheDocument()
    expect(screen.queryByText('Falha interna.')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Enviar ordem' })).toBeEnabled()
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it.each([
    ['JSON malformado', '{'],
    ['contrato inválido', JSON.stringify({ sucesso: true, exposicao_atual: '100', msg_erro: '' })],
  ])('releases submission after %s without retrying automatically', async (_reason, body) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(body, {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.type(screen.getByRole('textbox', { name: 'Quantidade' }), '1')
    await user.type(screen.getByRole('textbox', { name: 'Preço por ação' }), '1,00')
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    expect(await screen.findByRole('heading', { name: 'Falha de comunicação' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível confirmar a resposta')
    expect(screen.queryByText('Exposição atual')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Enviar ordem' })).toBeEnabled()
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
})
