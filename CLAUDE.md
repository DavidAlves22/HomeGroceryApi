# HomeGroceryApi — CLAUDE.md

API do **HomeGrocery** (compras de supermercado). .NET 9, Clean Architecture, PostgreSQL, JWT. O app é uma PWA online-only (repo `HomeGroceryApp`).

## Comandos
```
docker compose up -d banco            # só o PostgreSQL local (porta 5433)
dotnet run --project HomeGroceryApi.API   # API em desenvolvimento (Swagger em /swagger)
dotnet test                           # testes (rodar antes de dar algo por pronto)
dotnet tool run dotnet-ef migrations add <Nome> -p HomeGroceryApi.Infrastructure -s HomeGroceryApi.API -o Persistencia/Migrations
```
A API aplica as migrations ao subir (`Program.cs`).

## Camadas (regra de dependência: de fora para dentro)
- **Domain** — entidades, enums, regras puras, interfaces de repositório. Sem dependências.
- **Application** — casos de uso (uma classe por operação), DTOs, portas (`I*`), erros de negócio. Depende só do Domain.
- **Infrastructure** — EF Core/Npgsql, Identity, repositórios, token JWT. Implementa as portas.
- **CrossCutting** — composition root (`IoC/DependencyInjection.cs`), JWT, CORS, rate limit, Swagger.
- **API** — controllers finos + `TratadorDeErros` (único lugar que transforma erro de negócio em HTTP).
- **Tests** — xUnit com fakes em `Fakes/`.

## Decisões (resumo)
- **Grupo é dono dos dados.** Todo usuário nasce com um grupo pessoal (+ 11 categorias). Compartilhar com a família (Fase 4) = adicionar membros ao grupo.
- **Online-only**; o servidor é a fonte da verdade. `GET /saude` é o health check do app.
- **Histórico independente:** itens de lista guardam snapshots (nome, categoria, unidade); exclusões são lógicas (`ExcluidoEm`).
- **IDs GUID v7** gerados na aplicação. Concorrência otimista por `Versao` nos itens/listas (Fase 1+).
- Token de renovação: só o hash (SHA-512) fica no banco; rotação a cada uso.

## Convenções
- Nomes em português no domínio. Enums com sufixo `Enum` (`PapelNoGrupoEnum`).
- Assinaturas, parâmetros e chamadas em uma linha só.
- Casos de uso lançam `ErroDe*`; controllers não têm try/catch.
- Comentários explicam o porquê, não o quê.
- Sem segredos no repositório: `.env` e `appsettings.Production.json` são ignorados; `appsettings.Development.json` só tem valores locais.

## Hospedagem (custo ~zero)
API no Render (Docker) + PostgreSQL no Neon. Variáveis: `ConnectionStrings__Padrao`, `Jwt__ChaveSecreta`, `Cors__OrigensPermitidas__0` (URL da PWA na Vercel), `PORT` (definida pelo Render).

## Fase 1 (MVP) — decisões
- Lista padrão = `itens_do_modelo` por grupo (sem tabela "modelo": um grupo tem um só). Criar lista mensal **copia** os itens (ids novos + snapshot de nome/categoria/unidade). Teste: `Alterar_a_lista_padrao_nao_muda_listas_ja_criadas`.
- Uma lista **principal** por mês e grupo: índice único parcial `ux_lista_principal_por_mes`; a segunda exige `permitirDuplicada: true`.
- `ItemDaLista.Versao` é token de concorrência (EF) e também conferido no caso de uso: edição com versão velha → 409. Marcar/desmarcar é idempotente e não exige versão.
- Erros: `ErroDeDominio` → 422; validação → 400; não achou (ou é de outro grupo) → 404; conflito/concorrência/unique → 409.
- Todo caso de uso filtra por `IUsuarioAtual.GrupoId` (claim `grupo_id` do JWT). Nunca buscar por id sem o grupo.
- Casos de uso são registrados por varredura do namespace `Application.UseCases` (`AddCasosDeUso`).
- Testes de integração (`Tests/Integracao`) sobem a API em memória contra Postgres real em localhost:5433 (banco descartável por execução) e viram "ignorados" se o Postgres não estiver no ar.

## Fase 2 (financeiro) — decisões
- Dinheiro em `decimal` (nunca double). Cada linha é arredondada ao centavo (`MidpointRounding.AwayFromZero`) ANTES de somar: `CalculoDeTotais`.
- `PrecoEstimado` é referência; só `PrecoUnitario` pago, em item COMPRADO, entra no total gasto. Estimado nunca vira gasto sozinho. `QuantidadeComprada` (opcional) substitui a planejada no total.
- Preço fica no item da lista (snapshot do mês): comprar mais caro depois não altera meses anteriores. Não existe tabela separada de histórico de preços: as análises leem os próprios itens (`AnaliseDeCompras`, funções puras).
- Preços de g/ml são normalizados para kg/litro; unidades incompatíveis (pacote x kg) formam séries separadas.
- `Finalizar` congela `TotalGastoFinal`; `Reabrir` limpa. Resumos de lista finalizada usam o valor congelado.
- Endpoints: `GET /analises/gastos?meses=&competencia=` e `GET /analises/precos`.

## Fase 3 (experiência avançada) — decisões
- Origem `Copiar` cria a lista a partir de QUALQUER lista do grupo: itens novos, pendentes, sem quantidade comprada nem preço pago. O preço pago na origem vira `PrecoReferencia` (consulta), que nunca entra em totais. `PrecoEstimado` é mantido. Origem de outro grupo → 404.
- `PUT /listas/{id}/itens/ordem` guarda a ordem manual (corredores do mercado); cópias preservam a ordem. Lista finalizada recusa (422).
- Favoritos: `PUT /produtos/{id}/favorito`. Recorrentes = comprados em 2+ listas diferentes. `GET /produtos/sugestoes` devolve favoritos primeiro e depois recorrentes (máx. 12).
- `GET /produtos` sem `q` devolve TODOS (outros casos de uso, como excluir categoria, dependem disso); com `q` limita a 50.

## Fase 4 (família e tempo real) — decisões
- **Grupo ativo.** Um usuário pode ter vários vínculos (`MembroDoGrupo`), mas só um `Ativo`: o grupo do JWT. Entrar numa família desativa o vínculo pessoal (dados pessoais ficam guardados, nunca misturados); sair/ser removido reativa o pessoal, ou cria um novo se não houver (`ServicoDeGrupoAtivo`). Login e renovação sempre devolvem o grupo ativo.
- **Segurança:** `ValidarMembroDoGrupoMiddleware` confere a cada requisição autenticada (exceto `/autenticacao`) se o usuário é membro ATIVO do grupo do token; removido → 401 e o app renova a sessão. O hub aplica a mesma regra ao conectar.
- **Convites:** código de 8 caracteres (alfabeto sem O/0/I/1), uso único, 7 dias, só administrador gera. Códigos inexistentes, expirados e usados dão a MESMA resposta (404). `POST /grupo/entrar` usa o rate limit de autenticação. Só entra em família quem está sozinho no grupo atual.
- **Papéis:** Administrador convida, renomeia e remove (403 para membro). Se o único administrador sai, o membro mais antigo é promovido.
- **Tempo real:** SignalR em `/hub/grupo` (token em `access_token`). `UnidadeDeTrabalho.SalvarAsync` descobre pelo ChangeTracker o que mudou (itens/listas, catálogo, membros) e publica DEPOIS de gravar, então nenhuma operação esquece de avisar. Evento só carrega ids; o app busca o dado novo pela API. Falha ao avisar nunca derruba a operação.
- **Conflitos entre pessoas:** marcar/desmarcar é idempotente (vale a última); editar exige a versão do item (409 se outra pessoa mudou antes).
- Índice de vínculo ativo NÃO é único: o EF insere o novo antes de desativar o antigo.
