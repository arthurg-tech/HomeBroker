## Home Broker

### Convenções do desafio

- As ordens usam os ativos `PETR4`, `VALE3` e `VIIA4`, e os lados `C` (compra) e `V` (venda). Os rótulos Compra e Venda pertencem à interface.
- Quantidade deve ser um inteiro de 1 a 99.999. Preço deve ser um múltiplo de R$ 0,01 entre R$ 0,01 e R$ 999,99.
- As respostas usam `sucesso`, `exposicao_atual` e `msg_erro`, com campos numéricos JSON. Ordem aceita responde HTTP 200, limite excedido HTTP 422 e entrada inválida HTTP 400. Erros não alteram a exposição; quando não houver ativo válido identificável, `exposicao_atual` usa zero como convenção, não como consulta de saldo.
- A exposição começa em zero por ativo e é mantida em memória. Ordens aceitas são consideradas executadas integralmente; vendas podem deixar a exposição negativa. O estado se perde ao reiniciar o backend, e a aplicação será executada como uma instância.
- Cada POST válido representa uma ordem nova. Repetir a requisição contabiliza outra ordem; não há histórico persistente de ordens.
