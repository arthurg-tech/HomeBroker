import { useState, type FormEvent } from 'react'
import { sendOrder } from './orderApi'
import { ASSETS, validateOrderForm, type OrderFormValues, type OrderResponse } from './orderValidation'
import './App.css'

const initialForm: OrderFormValues = { ativo: 'PETR4', lado: 'Compra', quantidade: '', preco: '' }

function App() {
  const [form, setForm] = useState(initialForm)
  const [errors, setErrors] = useState<Partial<Record<keyof OrderFormValues, string>>>({})
  const [result, setResult] = useState<{ status: number; body: OrderResponse } | null>(null)
  const [requestError, setRequestError] = useState('')
  const [isSending, setIsSending] = useState(false)

  function updateField(field: keyof OrderFormValues, value: string) {
    setForm((previous) => ({ ...previous, [field]: value }))
    setErrors((previous) => ({ ...previous, [field]: undefined }))
    setResult(null)
    setRequestError('')
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const validation = validateOrderForm(form)

    if (!validation.order) {
      setErrors(validation.errors)
      setResult(null)
      setRequestError('')
      return
    }

    setErrors({})
    setResult(null)
    setRequestError('')
    setIsSending(true)
    try {
      setResult(await sendOrder(validation.order))
    } catch {
      setRequestError('Não foi possível confirmar a resposta. Confira a API antes de reenviar para evitar duplicar a ordem.')
    } finally {
      setIsSending(false)
    }
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <a className="brand" href="#inicio" aria-label="Home Broker, início">
          <span className="brand-mark" aria-hidden="true">H</span>
          <span className="brand-name">home<span>broker</span></span>
        </a>
        <span className="environment-label"><span className="status-dot" /> Ambiente de demonstração</span>
      </header>

      <main id="inicio" className="workspace">
        <div className="page-heading">
          <p className="eyebrow">MESA DE OPERAÇÕES <span>/</span> NOVA ORDEM</p>
          <h1>Enviar ordem</h1>
          <p className="page-description">Informe os dados da operação para atualizar a exposição financeira do ativo.</p>
        </div>

        <div className="content-grid">
          <section className="order-card" aria-labelledby="form-title">
            <div className="card-heading">
              <div><p className="card-kicker">ORDEM À VISTA</p><h2 id="form-title">Dados da ordem</h2></div>
              <span className="required-note"><span aria-hidden="true">*</span> Obrigatório</span>
            </div>

            <form noValidate onSubmit={handleSubmit}>
              <div className="field-grid">
                <div className="field">
                  <label htmlFor="asset">Ativo <span aria-hidden="true">*</span></label>
                  <select id="asset" name="ativo" value={form.ativo} required onChange={(event) => updateField('ativo', event.currentTarget.value as OrderFormValues['ativo'])}>
                    {ASSETS.map((asset) => <option key={asset} value={asset}>{asset}</option>)}
                  </select>
                </div>

                <div className="field">
                  <label htmlFor="side">Lado <span aria-hidden="true">*</span></label>
                  <select id="side" name="lado" value={form.lado} required onChange={(event) => updateField('lado', event.currentTarget.value as OrderFormValues['lado'])}>
                    <option value="Compra">Compra</option>
                    <option value="Venda">Venda</option>
                  </select>
                </div>

                <div className="field">
                  <label htmlFor="quantity">Quantidade <span aria-hidden="true">*</span></label>
                  <input id="quantity" name="quantidade" type="number" inputMode="numeric" min="1" max="99999" step="1" required
                    value={form.quantidade} placeholder="Ex.: 100" aria-invalid={Boolean(errors.quantidade)}
                    aria-describedby={errors.quantidade ? 'quantity-error' : 'quantity-hint'}
                    onChange={(event) => updateField('quantidade', event.currentTarget.value)} />
                  {errors.quantidade
                    ? <p className="field-error" id="quantity-error">{errors.quantidade}</p>
                    : <p className="field-hint" id="quantity-hint">De 1 a 99.999 ações</p>}
                </div>

                <div className="field">
                  <label htmlFor="price">Preço por ação <span aria-hidden="true">*</span></label>
                  <div className="input-with-prefix"><span aria-hidden="true">R$</span>
                    <input id="price" name="preco" type="text" inputMode="decimal" min="0.01" max="999.99" step="0.01" required
                      value={form.preco} placeholder="0,00" aria-invalid={Boolean(errors.preco)}
                      aria-describedby={errors.preco ? 'price-error' : 'price-hint'}
                      onChange={(event) => updateField('preco', event.currentTarget.value)} />
                  </div>
                  {errors.preco
                    ? <p className="field-error" id="price-error">{errors.preco}</p>
                    : <p className="field-hint" id="price-hint">De R$ 0,01 a R$ 999,99. Use vírgula ou ponto, sem separador de milhar.</p>}
                </div>
              </div>

              <div className="form-footer">
                <p className="security-note"><span aria-hidden="true">●</span> A API valida o limite antes de aceitar a ordem.</p>
                <button className="submit-button" type="submit" disabled={isSending}>
                  {isSending ? 'Enviando…' : 'Enviar ordem'}{!isSending && <span aria-hidden="true">↗</span>}
                </button>
              </div>
            </form>
          </section>

          <aside className="side-column" aria-label="Informações sobre a ordem">
            <section className="info-card">
              <div className="info-icon" aria-hidden="true">↗</div>
              <h2>Como funciona</h2>
              <p>Ordens aceitas atualizam a exposição financeira do ativo escolhido.</p>
              <div className="side-explanations">
                <p><span className="side-pill buy">C</span><span><strong>Compra</strong><small>Aumenta a exposição</small></span></p>
                <p><span className="side-pill sell">V</span><span><strong>Venda</strong><small>Diminui a exposição</small></span></p>
              </div>
              <div className="limit-note"><span aria-hidden="true">i</span><p>Limite de <strong>R$ 1.000.000</strong> por ativo, em valor absoluto.</p></div>
            </section>
            <div className="api-note"><span className="status-dot" /><p>API de ordens configurada</p></div>
          </aside>
        </div>

        {(result || requestError) && (
          <section className={`response-card ${result?.body.sucesso ? 'response-success' : 'response-error'}`}
            aria-labelledby="response-title" aria-live="polite">
            <div className="response-heading"><div><p className="card-kicker">RETORNO DO PROCESSAMENTO</p>
              <h2 id="response-title">{requestError ? 'Falha de comunicação' : 'Resposta da API'}</h2></div>
              {result && <span className="http-status">HTTP {result.status}</span>}
            </div>
            {requestError ? <p className="request-error" role="alert">{requestError}</p> : result && <>
              <p className="result-message" role="status"><span aria-hidden="true">{result.body.sucesso ? '✓' : '!'}</span>
                {result.body.sucesso ? 'Ordem aceita com sucesso.' : result.body.msg_erro}</p>
              <div className="response-value"><span>Exposição atual</span>
                <strong>{new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(result.body.exposicao_atual)}</strong>
              </div>
              <details className="json-details"><summary>Ver resposta JSON</summary>
                <pre>{JSON.stringify(result.body, null, 2)}</pre>
              </details>
            </>}
          </section>
        )}

        <footer className="page-footer">
          <span>HOME BROKER <span aria-hidden="true">·</span> ORDER GENERATOR</span>
          <span>Ordens sujeitas ao limite de exposição por ativo.</span>
        </footer>
      </main>
    </div>
  )
}

export default App
