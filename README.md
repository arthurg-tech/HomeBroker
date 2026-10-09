## Home Broker

### Convenções do desafio

- As ordens usam os ativos `PETR4`, `VALE3` e `VIIA4`, e os lados `C` (compra) e `V` (venda). Os rótulos Compra e Venda pertencem à interface.
- Quantidade deve ser um inteiro de 1 a 99.999. Preço deve ser um múltiplo de R$ 0,01 entre R$ 0,01 e R$ 999,99.
- As respostas usam `sucesso`, `exposicao_atual` e `msg_erro`, com campos numéricos JSON. Ordem aceita responde HTTP 200, limite excedido HTTP 422 e entrada inválida HTTP 400. Erros não alteram a exposição; quando não houver ativo válido identificável, `exposicao_atual` usa zero como convenção, não como consulta de saldo.
- A exposição começa em zero por ativo e é mantida em memória. Ordens aceitas são consideradas executadas integralmente; vendas podem deixar a exposição negativa. O estado se perde ao reiniciar o backend, e a aplicação será executada como uma instância.
- Cada POST válido representa uma ordem nova. Repetir a requisição contabiliza outra ordem; não há histórico persistente de ordens.

### API local

Execute na raiz do repositório:

```sh
dotnet run --project backend/OrderAccumulator --launch-profile http
```

A API recebe `POST http://localhost:5177/api/ordens` com `Content-Type: application/json` e os campos `ativo`, `lado`, `quantidade` e `preco`. Quantidade e preço devem ser números JSON; strings numéricas não são aceitas. Preços como `10` e `10.000` têm o mesmo valor e são aceitos; `10.001` é rejeitado. A interface deverá formatar valores monetários com duas casas.

As respostas 200/400/422 sempre incluem `sucesso`, `exposicao_atual` e `msg_erro`; no sucesso, a mensagem é vazia. Em uma entrada inválida com JSON válido e ativo permitido identificável, retorna-se a exposição atual dele. JSON malformado e ativo ausente ou inválido usam exposição zero por convenção. Erros de tipos e sintaxe JSON recebem mensagens sem detalhes internos ou stack traces.

Em desenvolvimento, o documento OpenAPI está em `http://localhost:5177/openapi/v1.json`, e o CORS permite a origem `http://localhost:5173`. O Vite está configurado para usar essa porta sem escolher outra automaticamente. O perfil HTTPS existente usa `https://localhost:7055`; o perfil HTTP acima permite testar localmente sem certificado.

Os exemplos reproduzíveis estão em [docs/requests.http](docs/requests.http). Execute-os na ordem indicada após reiniciar o backend para começar com exposições zeradas.

### Testes

```sh
dotnet test HomeBroker.slnx
```

O projeto `backend/OrderAccumulator.Tests` organiza os testes nas pastas `Unit` (validação e serviço de exposição, incluindo concorrência) e `Integration` (API HTTP). Os testes de integração usam `WebApplicationFactory` para executar o pipeline real do ASP.NET Core em memória, com uma aplicação isolada por caso de teste.

Para iniciar o frontend, copie `frontend/OrderGenerator/.env.example` para `frontend/OrderGenerator/.env`, mantenha a API em execução e rode `npm --prefix frontend/OrderGenerator run dev`. A tela aceita vírgula ou ponto como separador decimal de preço e não aceita separador de milhar. Para validar o frontend, rode `npm --prefix frontend/OrderGenerator test` e `npm --prefix frontend/OrderGenerator run build`.
