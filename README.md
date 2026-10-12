# Home Broker

Aplicação para enviar ordens de compra e venda e calcular a exposição financeira de cada ativo, rejeitando operações que ultrapassem o limite definido.

> This is a challenge by [Coodesh](https://coodesh.com/)

## Tecnologias

- **Backend (OrderAccumulator):** C#, .NET 10 e ASP.NET Core Minimal API, com contrato REST/JSON e OpenAPI.
- **Frontend (OrderGenerator):** TypeScript, React 19, Vite 8 e CSS.
- **Testes backend:** xUnit e WebApplicationFactory para integração HTTP.
- **Testes frontend:** Vitest, Testing Library e jsdom; ESLint para análise estática.
- **Execução local:** npm e concurrently para iniciar os dois processos juntos.

## Pré-requisitos

- Git para clonar o repositório.
- SDK .NET 10.
- Node.js 24.15.0 ou posterior da linha 24, com npm, compatível com as dependências dos arquivos de lock.

Ambiente usado nas verificações do projeto: SDK .NET 10.0.112, Node.js 24.21.0 e npm 11.19.0.

## Instalação

Clone o repositório e entre na pasta:

```sh
git clone https://github.com/arthurg-tech/HomeBroker.git
cd HomeBroker
```

Execute os comandos abaixo na raiz do repositório:

```sh
npm ci
npm --prefix frontend/OrderGenerator ci
dotnet restore HomeBroker.slnx
```

`npm ci` instala as versões registradas nos arquivos `package-lock.json`. Há dependências na raiz, para a execução conjunta, e no frontend. A primeira instalação precisa de acesso aos registros npm e NuGet.

### Configuração da API no frontend

Sem configuração adicional, o frontend usa `http://localhost:5177`.

Para definir outro endereço, copie [frontend/OrderGenerator/.env.example](frontend/OrderGenerator/.env.example) para `frontend/OrderGenerator/.env` usando seu editor ou gerenciador de arquivos e ajuste:

```dotenv
VITE_API_BASE_URL=http://localhost:5177
```

Reinicie o Vite após alterar o arquivo. Para um build, defina o endereço antes de executar o comando de build: variáveis `VITE_*` são incorporadas ao frontend e são públicas, portanto não devem conter segredos.

## Execução local

Todos os comandos desta seção devem ser executados na raiz do repositório.

### Backend e frontend juntos

```sh
npm run dev
```

| Serviço | Endereço |
|---|---|
| OrderGenerator | http://localhost:5173 |
| OrderAccumulator | http://localhost:5177 |
| Swagger UI | http://localhost:5177/swagger |
| Documento OpenAPI | http://localhost:5177/openapi/v1.json |

Abra `http://localhost:5173` no navegador. `Ctrl+C` encerra os dois processos. O comando usa `concurrently` e funciona em Windows/Linux.

### Serviços separados

No primeiro terminal, inicie a API:

```sh
dotnet run --project backend/OrderAccumulator --launch-profile http
```

No segundo terminal, inicie o frontend:

```sh
npm --prefix frontend/OrderGenerator run dev
```

O perfil HTTP permite testar localmente sem certificado. O perfil HTTPS existente usa `https://localhost:7055`.

### Documentação interativa da API

Com a API em execução, abra [Swagger UI](http://localhost:5177/swagger) para consultar os modelos, os status de resposta e testar `POST /api/ordens` com **Try it out** e **Execute**. Os envios processam ordens e atualizam a exposição em memória, assim como os envios pelo frontend.

A interface usa o documento existente em `/openapi/v1.json`. Tanto o Swagger UI quanto o documento OpenAPI ficam disponíveis apenas no ambiente `Development`, já configurado nos perfis locais `http` e `https`.

## Como usar

1. Escolha PETR4, VALE3 ou VIIA4 e o lado Compra ou Venda.
2. Informe uma quantidade inteira entre 1 e 99.999 e um preço entre R$ 0,01 e R$ 999,99.
3. Clique em **Enviar ordem**. A página apresenta o status HTTP, a exposição e, em caso de erro, a mensagem da API. **Ver resposta JSON** mostra o retorno completo.

O preço aceita vírgula ou ponto como separador decimal, até duas casas decimais e sem separador de milhar.

Para conferir uma sequência, inicie o backend com estado zerado e envie uma compra de 584 ações de PETR4 a R$ 54,87: a exposição será R$ 32.044,08. Em seguida, envie uma venda de 100 ações a R$ 50,00: a exposição será R$ 27.044,08.

Em caso de falha de comunicação, confira a API antes de reenviar: a ordem pode ter sido processada mesmo sem a confirmação chegar ao navegador. Cada envio válido contabiliza uma nova ordem; o frontend não repete automaticamente uma requisição.

Uma resposta **HTTP 429** aparece como **Limite de requisições atingido**: a ordem não foi processada e as exposições foram preservadas. Aguarde antes de tentar enviar novamente. Se a API informar `Retry-After` em segundos, a interface mostra a espera estimada. A exposição atual fica oculta nessa resposta, pois o zero do JSON é uma convenção de erro, sem consulta do ativo; o retorno completo continua disponível em **Ver resposta JSON**. O reenvio é manual.

## Regras e decisões

- Ativos permitidos: `PETR4`, `VALE3` e `VIIA4`. A API usa `C` para compra e `V` para venda; a interface mostra Compra/Venda.
- Quantidade: inteiro de 1 a 99.999. Preço: múltiplo de R$ 0,01 entre R$ 0,01 e R$ 999,99. Valores inválidos não são arredondados para serem aceitos.
- Exposição por ativo: soma de preço × quantidade das compras menos a soma de preço × quantidade das vendas. Vendas podem produzir exposição negativa.
- Limite inclusivo: de -R$ 1.000.000 a +R$ 1.000.000 por ativo. Rejeições preservam a exposição anterior.
- Ordens aceitas são executadas integralmente. Não há execução parcial ou casamento de ordens.
- Estado em memória, compartilhado por um serviço Singleton e protegido por `lock`. O backend deve executar em uma única instância; o bloqueio não coordena múltiplos processos.
- A exposição começa em zero e se perde ao reiniciar o backend. Não há persistência de histórico, autenticação ou deduplicação de POST.

## Contrato da API

`POST http://localhost:5177/api/ordens`, com `Content-Type: application/json`:

```json
{
  "ativo": "PETR4",
  "lado": "C",
  "quantidade": 584,
  "preco": 54.87
}
```

Resposta a partir de exposição zero:

```json
{
  "sucesso": true,
  "exposicao_atual": 32044.08,
  "msg_erro": ""
}
```

| Status HTTP | Resultado | Exposição |
|---|---|---|
| 200 | Ordem aceita; `sucesso: true`, mensagem vazia. | Atualizada. |
| 400 | Entrada inválida; `sucesso: false`, mensagem explicativa. | Preservada. |
| 422 | Limite excedido; `sucesso: false`, mensagem explicativa. | Preservada. |
| 429 | Cota de requisições excedida; `sucesso: false`, mensagem explicativa. | Preservada; resposta usa zero por convenção. |

As respostas previstas sempre contêm `sucesso`, `exposicao_atual` e `msg_erro`. Em uma entrada inválida com JSON válido dentro do limite de tamanho e ativo permitido identificável, a API retorna a exposição atual dele. JSON malformado, corpo acima do limite e ativo ausente ou inválido usam exposição zero como convenção de erro, sem consultar um saldo válido. Mensagens de erro não expõem detalhes internos ou stack traces.

### Rate limiting de ordens

`POST /api/ordens` usa uma cota por IP com janela deslizante, sem fila de espera. A configuração padrão permite **60 requisições em uma janela de 60 segundos**, dividida em seis segmentos de dez segundos. Em `Development`, a cota é **120 requisições**, com a mesma janela. Esses são valores iniciais configuráveis, não limites definidos por benchmark.

As opções ficam em `RateLimiting:Orders` nos arquivos de configuração do backend:

```json
{
  "RateLimiting": {
    "Orders": {
      "PermitLimit": 60,
      "WindowSeconds": 60,
      "SegmentsPerWindow": 6
    }
  }
}
```

Os três valores devem ser inteiros positivos; `SegmentsPerWindow` não pode exceder `WindowSeconds`, para que cada segmento tenha pelo menos um segundo. Uma configuração inválida impede a inicialização. Variáveis de ambiente também podem sobrescrever as opções, por exemplo `RateLimiting__Orders__PermitLimit`.

Toda requisição que chega ao limitador nesse endpoint consome a cota quando admitida, inclusive ordens que depois retornam 400 ou 422. Preflights CORS e a documentação Swagger/OpenAPI não consomem essa cota.

Quando a cota é excedida, a API retorna **HTTP 429** antes de ler o corpo ou processar a ordem:

```json
{
  "sucesso": false,
  "exposicao_atual": 0,
  "msg_erro": "Limite de requisições excedido. Aguarde antes de enviar outra ordem."
}
```

A exposição de todos os ativos é preservada. O zero da resposta é uma convenção de erro, pois o ativo não foi identificado. Quando disponível, `Retry-After` informa a estimativa de espera em segundos inteiros, arredondada para cima; o CORS de desenvolvimento expõe esse header para o frontend. A estimativa não reserva uma vaga para um envio futuro.

A cota fica em memória, por processo, e é reiniciada junto com o backend. Clientes com o mesmo IP compartilham a cota; requisições sem IP identificado compartilham uma cota de fallback. O IP vem da conexão, sem interpretar diretamente `X-Forwarded-For`. Uma implantação atrás de proxy precisa configurar forwarded headers com proxies confiáveis antes do roteamento e do limitador.

### Limite do corpo da ordem

O corpo de `POST /api/ordens` aceita até **1.000 bytes UTF-8**, incluindo espaços e campos adicionais. O limite é inclusivo e aplicado com ou sem `Content-Length`, inclusive em envios chunked. Há margem para os quatro campos da ordem sem permitir que documentos grandes sejam carregados integralmente pelo parser.

Um `Content-Length` acima do limite permite rejeição antecipada; nos demais casos, a aplicação lê no máximo 1.001 bytes para detectar o excesso antes de criar o documento JSON. Um corpo maior retorna **HTTP 400** para preservar os status do contrato atual, com o seguinte JSON, sem processar a ordem ou alterar a exposição de qualquer ativo:

```json
{
  "sucesso": false,
  "exposicao_atual": 0,
  "msg_erro": "O corpo da ordem excede o tamanho permitido"
}
```

Esse zero é uma convenção de erro: o corpo rejeitado não foi desserializado e não identifica uma exposição consultada. Limites do servidor ou proxy continuam independentes dessa proteção aplicada pelo endpoint.

Quantidade e preço devem ser números JSON; strings numéricas, booleanos, null e campos obrigatórios ausentes são rejeitados. No JSON, preços `10` e `10.000` representam o mesmo valor e são aceitos; `10.001` é rejeitado. A interface aplica sua regra textual de até duas casas e formata valores monetários em reais com duas casas.

Outros exemplos estão em [docs/requests.http](docs/requests.http). Use um cliente compatível com arquivos `.http`, como a extensão REST Client do VS Code, e execute os exemplos na ordem indicada após reiniciar o backend para zerar as exposições.

## Estrutura do projeto

| Caminho | Responsabilidade |
|---|---|
| `backend/OrderAccumulator` | API, contratos, validação e serviço de exposição. |
| `backend/OrderAccumulator.Tests/Unit` | Testes do validador e do serviço, incluindo concorrência. |
| `backend/OrderAccumulator.Tests/Integration` | Testes HTTP do pipeline ASP.NET Core. |
| `frontend/OrderGenerator` | Formulário React, validação local e comunicação com a API. |
| `frontend/OrderGenerator/src` | Código e assets da aplicação frontend. |
| `frontend/OrderGenerator/tests` | Testes frontend e setup compartilhado do Vitest. |
| `HomeBroker.slnx` | Solução .NET na raiz do repositório. |
| `docs/requests.http` | Exemplos reproduzíveis de requisições. |

## Testes e verificações

Na raiz do repositório:

```sh
dotnet test HomeBroker.slnx -m:1
npm --prefix frontend/OrderGenerator test
npm --prefix frontend/OrderGenerator run lint
npm --prefix frontend/OrderGenerator run build
```

Para executar os testes frontend em modo contínuo durante o desenvolvimento:

```sh
npm --prefix frontend/OrderGenerator run test:watch
```

Os testes HTTP usam `WebApplicationFactory` com uma aplicação nova por cenário; sequências e requisições concorrentes compartilham o mesmo host. O Vitest executa os testes frontend de `tests/` em jsdom, com respostas simuladas da API. As suítes podem executar sem iniciar os serviços manualmente.

O build do frontend verifica os tipos da aplicação, das configurações e dos testes antes de gerar os arquivos de produção em `frontend/OrderGenerator/dist`.

Na última verificação da Fase 09, passaram 120 testes backend, 48 testes frontend, lint e build. Esses números registram aquela execução.

## Arquivos ignorados

O [.gitignore](.gitignore) inclui `bin`, `obj`, `node_modules`, `dist`, configurações locais e logs. Arquivos `.env` são ignorados; `.env.example` e os arquivos de lock são mantidos no repositório para configuração e instalação reproduzível.

## Entrega e apresentação

Conforme as instruções do desafio:

1. Adicione o link deste repositório com a solução na questão da plataforma.
2. Revise o README e faça o commit final antes da entrega.
3. Envie a solução e aguarde as próximas instruções. Se houver apresentação em vídeo, a gravação estará disponível na tela de entrega após adicionar o link do repositório.

Para dúvidas sobre o processo, entre em contato com um especialista pelo chat da plataforma Coodesh.
