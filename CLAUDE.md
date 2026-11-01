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
