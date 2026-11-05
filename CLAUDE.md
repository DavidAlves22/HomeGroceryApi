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
- **Grupo é dono dos dados.** Todo usuário nasce com um grupo pessoal (+ as categorias de `CategoriasIniciais`, inclusive os corredores do mapa de mercado). Compartilhar com a família (Fase 4) = adicionar membros ao grupo.
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

## Fase 5 (mercados, mapas e rota) — decisões
- **N mercados por grupo** (`Loja`), cada um com mapa em grade (`Largura` x `Altura` células, ~1,5 m cada), entrada e caixas. Exclusão lógica: itens antigos guardam `CompradoNaLojaId`.
- **Mapa salvo por inteiro** (`PUT /lojas/{id}/mapa`): seções antigas saem, novas entram com ids novos (a resposta traz as ids). `Versao` da loja evita sobrescrita entre duas pessoas (409). Validação em `ValidadorDoMapa` (dentro da grade, sem sobreposição, gôndola dupla com 2+ células de espessura).
- **Gôndola de dois lados**: `Secao.Dividida` + `SecaoCategoria.Lado` (A = cima/esquerda, B = baixo/direita). A parada fica só no corredor do lado do item (+ pontas da metade).
- **Categoria em vários lugares**: NÃO há unicidade por loja+categoria; só não repete no mesmo lado da mesma seção. A rota vai ao lugar mais próximo.
- **Compra dividida**: `ItemDaLista.CompradoNaLojaId` (enviado em `PUT .../comprado` com `lojaId`) e `LojasSemOItem` ("Não tem aqui", `PUT .../sem-na-loja`; continua pendente). Desmarcar limpa o mercado.
- **Rota** (`POST /listas/{id}/rota` com `lojaId` e posição opcional): só itens NÃO comprados e que não estão em `LojasSemOItem` desse mercado. Sem seção para a categoria → `foraDaRota` ("SemSecao"); marcados "Não tem aqui" → "NaoTemNaLoja". Algoritmo em `Domain/Servicos/PlanejadorDeRota` (Dijkstra 8 direções, sem cortar quina, vizinho mais próximo); a economia compara com seguir a ordem das categorias. Sem GPS: a posição vem do app (último item marcado).
- **Local do produto** (`LocalDoProduto`, `PUT /listas/{id}/itens/{itemId}/local`): onde o produto foi achado num mercado. Chave = nome normalizado (`AnaliseDeCompras.Normalizar`), então vale para o grupo e para listas futuras. Até 5 locais por produto+mercado (a rota vai ao mais perto); lista vazia apaga. Vence a categoria da seção: no planejador, o item com local próprio vira um grupo só dele (chave = id do item). Como o mapa é salvo por inteiro, `SecaoRequisicao.IdAnterior` faz o local acompanhar a seção que continua (sem ele, o local é descartado). `POST /lojas` com cópia leva os locais junto. A rota devolve `locaisProprios`.
- Tempo real: mudança em loja/seção/local do produto avisa o grupo com o evento `lojas`.
- `LojasSemOItem` é `uuid[]`: o domínio troca o array inteiro para o EF perceber a mudança.

## Publicação
- Guia completo em `docs/PUBLICACAO.md` (Neon + Render + Vercel). `render.yaml` é o blueprint do Render; `vercel.json` (repo do app) define build/saída/headers.
- `StringDeConexao.Normalizar` aceita a URL `postgresql://...` do Neon.
- `UseForwardedHeaders` confia no proxy da plataforma (KnownNetworks/KnownProxies limpos): sem isso o rate limit por IP seria compartilhado por todos. Verificado com o contêiner (12 IPs distintos passam; o mesmo IP leva 429 após 10).
- Imagem Docker validada localmente em modo Production (porta via `PORT`, migrations ao subir, CORS só da origem configurada, Swagger desligado).

## Fase 6 (compra em família) — decisões
- **Divisão por categoria** (`PUT /listas/{id}/divisao`): coluna `Divisao` (jsonb em texto) na lista, vale só para ela. Item de categoria sem responsável (ou cujo responsável saiu do grupo) é de todos. `RotaRequisicao.UsuarioId` filtra a rota; a resposta traz `comOutrasPessoas` e `rotasDeOutrasPessoas` (posição = lugar do último item que a pessoa marcou no mercado, senão a entrada; mais o trajeto que falta para ela).
- **Parada no centro da gôndola** (`PlanejadorDeRota.ParadaNoCentro`): no meio do lado comprido; as pontas só se esse corredor não for alcançável. Caixas continuam no ponto mais próximo.
- **Onde achou**: um único local por produto e mercado (`DefinirLocalDoItem` recusa mais de um).
- **Cancelar compra** (`DELETE /listas/{id}`): só em andamento; apaga lista e itens.
- **Avisos por Web Push** (gratuito): `NotificadorDePush` (pacote Lib.Net.Http.WebPush) com chaves VAPID em `Push__ChavePublica`, `Push__ChavePrivada`, `Push__Assunto`. SEM as chaves fica desligado (`GET /notificacoes/chave` dá 404 e o app esconde o botão). `UnidadeDeTrabalho` decide os avisos olhando o ChangeTracker: item novo numa lista que já existia (criar a lista não avisa) e compra finalizada; vão para os outros membros ATIVOS do grupo, nunca para quem fez. Assinaturas em `assinaturas_de_push` (endpoint único; muda de dono se outra conta entra no aparelho); 404/410 do serviço de push apaga a assinatura. Testes usam `NotificadorDePushDeTeste` (fake que registra os avisos).
- **Nota fiscal (NFC-e)** (`POST /listas/{id}/nota`, `GET /notas/vinculos?cnpj=`): o app lê o texto colado da nota; a API só recebe os itens já lidos. Vale também em lista FINALIZADA (o jeito normal é importar em casa, uma nota por mercado): os itens casados recebem `QuantidadeComprada` e `PrecoUnitario` da nota e viram comprados (`ItemDaLista.RegistrarCompra`, a nota sobrescreve); os que não estavam na lista entram já comprados (não geram aviso de "pediu um item"); o `TotalGastoFinal` congelado é recalculado na mesma gravação. `NotaImportada.Impressao` (SHA-256 de CNPJ + código/quantidade/preço de cada item) barra a mesma nota duas vezes (409). `VinculoDeProdutoDaNota` (grupo + CNPJ + código do mercado -> produto) é aprendido a cada importação; `Loja.Cnpj` também (só dígitos), para a próxima nota daquele CNPJ abrir no mercado certo. Duas linhas da nota para o mesmo item viram uma compra com preço médio. NÃO há leitura automática do portal da SEFAZ: o firewall dele barra robôs e burlar isso não é aceitável.
- **Chave de acesso no QR** (`ImportarNotaRequisicao.ChaveDeAcesso`, `GET /notas/importada?chave=`): quando a nota foi lida pelo QR, a chave (44 dígitos, CNPJ nos dígitos 7 a 20) é guardada em `NotaImportada` (índice único por grupo) e reconhece a mesma nota antes de colar o texto (409 também na importação). Chave com CNPJ diferente do informado é recusada (400); sem CNPJ no pedido, usa o da chave.

## Conta e recuperação de senha — decisões
- `PUT /conta/nome` (autenticado) troca o nome; aparece para a família.
- Recuperação por código de 6 dígitos no e-mail: `GET /autenticacao/recuperacao` ({disponivel}), `POST /autenticacao/esqueci-senha` (sempre 204, exista ou não a conta), `POST /autenticacao/redefinir-senha` (204 ou 400 genérico). Só o hash do código fica no banco; validade 15 min; 5 tentativas; um código ativo por conta; senha fraca não gasta o código; ao redefinir, todas as sessões (tokens de renovação) são revogadas. Usa o rate limit de autenticação.
- E-mail via MailKit (`EnviadorDeEmailSmtp`, variáveis `Email__*`, ver `docs/PUBLICACAO.md` 4c). Testes usam `EnviadorDeEmailDeTeste`.
- **Login com Google** (`POST /autenticacao/login-google` {idToken}, `GET /autenticacao/google` -> {clienteId}): o app (PWA) usa o botão do Google Identity Services e manda o ID token; `ValidadorDeLoginGoogle` (Google.Apis.Auth) confere assinatura, emissor, validade, audiência (`Google__ClienteId`, aceita vários separados por vírgula) e e-mail verificado; sem Client ID nenhum token vale. Conta com o mesmo e-mail entra nela; e-mail novo cria conta + grupo pessoal com senha aleatória desconhecida (quem quiser senha usa "Esqueci minha senha"). Testes usam `ValidadorDeLoginGoogleDeTeste` (token "ok|email|nome"). Ver `docs/PUBLICACAO.md` 4d.
- **Catálogo de mapas** (`GET /catalogo/mercados?busca=`, `GET /catalogo/mercados/{id}`, `POST /catalogo/mercados/{id}/copiar`, `PUT /lojas/{id}/compartilhamento`): `Loja.Compartilhada` (padrão true) deixa o mapa à vista de OUTROS grupos (só leitura). Aparece no catálogo se estiver compartilhado, não excluído, com seções e (não for cópia OU já tiver sido editado: `OrigemId == null || Versao > 1`, para não encher de repetidos). Ordem: mais copiados, depois nome. Não revela quem criou; categorias vão pelo NOME. Copiar cria uma `Loja` no grupo de quem pede (`OrigemId` guarda a origem), procura as categorias por nome e CRIA as que faltam; nome repetido ganha " (2)"; locais de produto NÃO vão. Cópia independente: editar a cópia nunca altera o original. Testes: `CatalogoDeMapasTests`.
- **Tamanho do mapa**: `ValidadorDoMapa.TamanhoMinimo` 10 e `TamanhoMaximo` 120 células por lado (o app cria 34 x 40 e o editor permite mudar). Máx. 150 seções.
- **Cidade do mercado**: `Loja.Cidade` (obrigatória em `POST /lojas`, salva com o mapa em `PUT /lojas/{id}/mapa`; ao copiar de mercado do grupo vale a dele se não vier) e `CidadeBusca` (minúsculas e sem acento) para o filtro `?cidade=` do catálogo. Mercado sem cidade não aparece no catálogo.
- **Excluir categoria** (`ExcluirCategoria`): além de mover produtos/itens do modelo para "Outros", remove a categoria de TODAS as seções dos mapas do grupo (`ILojaRepository.RemoverVinculosDaCategoriaAsync`). Sem isso o mapa não salvava ("usa uma categoria que não existe"). A migration `LimparCategoriasExcluidasDosMapas` soltou os vínculos antigos de categorias já excluídas.
- **Quem entra no catálogo** (`Loja.MinimoDeSecoesParaOCatalogo` = 8, `Loja.NomePadraoDaSecao` = "Nova seção"): sem interruptor de compartilhamento (a coluna `Compartilhada` e `PUT /lojas/{id}/compartilhamento` foram removidos). Elegível = não excluído + cidade + ao menos 8 seções cujo nome (sem espaços/caixa) não é "Nova seção" + (não ser cópia ou já ter sido editado). Uma consulta só (`RepositorioDeLojas.ElegiveisParaOCatalogo`) serve a lista, o detalhe e a cópia, e o contador de seções do catálogo é o de seções nomeadas.

## Painel do administrador — decisões
- **Quem é admin:** e-mail listado em `Admin:Emails` (variáveis `Admin__Emails__0`, `Admin__Emails__1`...), comparado sem diferenciar caixa e sem espaços. Sem a lista ninguém é admin. O papel vira a claim `role=Admin` no JWT (`TokenService`, via `IAdministradores`); `RoleClaimType = "role"` em `ConfiguracoesIniciais` (por causa de `MapInboundClaims = false`). Nenhum campo do cliente dá esse papel. `SessaoResposta.EhAdmin` só avisa o app para mostrar o atalho.
- **Cadastro não verifica e-mail:** cadastre a conta do admin ANTES de pôr o e-mail em `Admin__Emails__0` (ou entre pelo Google), senão outra pessoa poderia criar essa conta primeiro. O papel só vale até o token expirar (30 min) depois de remover alguém da lista.
- **Rotas** (`AdminController`, `[Authorize(Roles = "Admin")]`, 403 para os demais): `GET /admin/resumo`, `GET /admin/series?dias=` (7 a 90, padrão 30; dias de Brasília, UTC-3 fixo), `GET /admin/usuarios?pagina=` (50 por página). Leitura em `IConsultasDeAdmin`/`ConsultasDeAdmin`: só contagens, somas e a lista de contas; nunca o conteúdo das listas das famílias. É a única porta que olha além do grupo do usuário atual.
- **Uso medido:** `ApplicationUser.CriadoEm` e `UltimoAcessoEm`. O acesso é gravado em toda emissão de sessão (login, cadastro, Google, renovação; o app renova a cada ~30 min de uso), no máximo uma vez a cada 5 min (`ServicoDeContas.RegistrarAcessoAsync`). Migration `PainelAdminContaCriadaEUltimoAcesso` preenche `CriadoEm` das contas antigas com a entrada mais antiga em grupo. Não há tabela de eventos: "tela visitada" e "tempo de uso" não existem.
- Testes: `PainelAdminTests` (401/403/200, números, séries, lista de contas) e `AutenticacaoTests`. `ApiFactory` define `Admin__Emails__0 = dono@teste.com`.
- **Bloquear conta** (`PUT /admin/usuarios/{id}/bloqueio` {bloqueado}, `DefinirBloqueioDeConta`): `ApplicationUser.BloqueadoEm` (migration `BloqueioDeConta`). Bloquear revoga todos os tokens de renovação e vale na hora: `ValidarMembroDoGrupoMiddleware` e o `GrupoHub` consultam `IServicoDeContas.EstaBloqueadaAsync` a cada requisição/conexão (401 → o app tenta renovar → recusado → volta ao login). `EmissorDeSessao` recusa conta bloqueada em TODA entrada (login, Google, renovação) com "Esta conta foi bloqueada..."; com senha errada a mensagem continua a genérica (não revela a conta). Administrador (e-mail em `Admin:Emails`) não pode ser bloqueado (400), o que impede o dono de se trancar fora. `GET /admin/usuarios` traz `bloqueadoEm` e `ehAdmin` por conta. Quem já estava conectado no tempo real só cai ao reconectar (o hub só empurra ids de eventos do grupo). Testes: `PainelAdminTests`.
- **Insights** (`GET /admin/insights`, `ConsultasDeAdmin.InsightsAsync`), tudo em consultas, sem tabela nova: retenção (contas criadas há ≥1/7/30 dias cujo `UltimoAcessoEm` é ≥ `CriadoEm` + N dias; contas antigas só contam depois de entrarem de novo), funil (contas, criaram lista, compraram item, finalizaram compra, sem atividade), gasto (média e total por mês das compras finalizadas, 6 meses), mais comprados (10 produtos agrupados por nome em minúsculas, com "em quantas casas"; 8 categorias), mercados (cidades e mapas mais copiados) e adoção de recursos (família, divisão, mercado, nota, avisos). Só nomes de produto/mercado e contagens; nunca quem comprou.
