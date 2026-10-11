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

    const quantity = screen.getByRole('spinbutton', { name: 'Quantidade' })
    expect(quantity).toBeRequired()
    expect(quantity).toHaveAttribute('min', '1')
    expect(quantity).toHaveAttribute('max', '99999')
    expect(quantity).toHaveAttribute('step', '1')

    const price = screen.getByRole('textbox', { name: 'Preço por ação' })
    expect(price).toBeRequired()
    expect(price).toHaveAttribute('inputmode', 'decimal')
    expect(price).toHaveAttribute('min', '0.01')
    expect(price).toHaveAttribute('max', '999.99')
    expect(price).toHaveAttribute('step', '0.01')
  })

  it('shows errors next to blank fields without converting them to zero or posting', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    expect(await screen.findByText('Informe a quantidade.')).toBeInTheDocument()
    expect(screen.getByText('Informe o preço por ação.')).toBeInTheDocument()
    expect(screen.getByRole('spinbutton', { name: 'Quantidade' })).toHaveValue(null)
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('lets a keyboard user reach every form field and submit', async () => {
    const user = userEvent.setup()
    render(<App />)
    const focusOrder = [
      screen.getByRole('link', { name: 'Home Broker, início' }),
      screen.getByRole('combobox', { name: 'Ativo' }),
      screen.getByRole('combobox', { name: 'Lado' }),
      screen.getByRole('spinbutton', { name: 'Quantidade' }),
      screen.getByRole('textbox', { name: 'Preço por ação' }),
      screen.getByRole('button', { name: 'Enviar ordem' }),
    ]

    for (const element of focusOrder) {
      await user.tab()
      expect(element).toHaveFocus()
    }

    await user.keyboard('{Enter}')
    expect(await screen.findByText('Informe a quantidade.')).toBeInTheDocument()
    expect(screen.getByText('Informe o preço por ação.')).toBeInTheDocument()
  })

  it.each([
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
    await user.type(screen.getByRole('spinbutton', { name: 'Quantidade' }), '584')
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

  it.each([
    ['fração de centavo', '10,001'],
    ['separadores misturados', '1.234,56'],
    ['abaixo do mínimo', '0'],
    ['acima do máximo', '1000'],
    ['campo vazio', ''],
  ])('blocks a price with %s and does not round or post it', async (_reason, price) => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.type(screen.getByRole('spinbutton', { name: 'Quantidade' }), '1')
    if (price) await user.type(screen.getByRole('textbox', { name: 'Preço por ação' }), price)
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    expect(await screen.findByText(price ? /sem arredondar/ : 'Informe o preço por ação.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('blocks fractional and out-of-range quantities', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)
    const quantity = screen.getByRole('spinbutton', { name: 'Quantidade' })

    await user.type(quantity, '1.5')
    await user.type(screen.getByRole('textbox', { name: 'Preço por ação' }), '1,00')
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))
    expect(await screen.findByText(/número inteiro/)).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()

    await user.clear(quantity)
    await user.type(quantity, '100000')
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))
    expect(await screen.findByText(/entre 1 e 99\.999/)).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
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

    await user.type(screen.getByRole('spinbutton', { name: 'Quantidade' }), '1')
    await user.type(screen.getByRole('textbox', { name: 'Preço por ação' }), '10,00')
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    expect(await screen.findByText(`HTTP ${status}`)).toBeInTheDocument()
    expect(screen.getByText(message)).toBeInTheDocument()
    await user.click(screen.getByText('Ver resposta JSON'))
    expect(screen.getByText((_content, element) => element?.tagName === 'PRE'))
      .toHaveTextContent(`"exposicao_atual": ${exposure}`)
    expect(screen.getByRole('button', { name: 'Enviar ordem' })).toBeEnabled()
  })

  it('shows the pending state and disables submission until the API responds', async () => {
    let resolveResponse!: (response: Response) => void
    const fetchMock = vi.fn(() => new Promise<Response>((resolve) => {
      resolveResponse = resolve
    }))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.type(screen.getByRole('spinbutton', { name: 'Quantidade' }), '2')
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

    await user.type(screen.getByRole('spinbutton', { name: 'Quantidade' }), '2')
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

    await user.type(screen.getByRole('spinbutton', { name: 'Quantidade' }), '2')
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

    await user.type(screen.getByRole('spinbutton', { name: 'Quantidade' }), '1')
    await user.type(screen.getByRole('textbox', { name: 'Preço por ação' }), '1,00')
    await user.click(screen.getByRole('button', { name: 'Enviar ordem' }))

    expect(await screen.findByRole('heading', { name: 'Falha de comunicação' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível confirmar a resposta')
    expect(screen.queryByText('Exposição atual')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Enviar ordem' })).toBeEnabled()
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
})
