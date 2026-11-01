# HomeGrocery API

API do HomeGrocery: listas de compras mensais, lista padrão, histórico, gastos e compartilhamento familiar.

## Rodando localmente
1. Copie `.env.example` para `.env` e defina `POSTGRES_PASSWORD=dev-local-apenas` (a mesma senha de `appsettings.Development.json`), depois `docker compose up -d banco` (PostgreSQL na porta 5433).
2. Para outro valor de senha, altere os dois lugares (ou use `dotnet user-secrets`).
3. `dotnet run --project HomeGroceryApi.API` e abra `/swagger`.

## Endpoints (Fase 0)
| Método | Rota | Descrição |
|---|---|---|
| GET | `/saude` | Health check (API + banco) |
| POST | `/autenticacao/cadastro` | Cria conta, grupo pessoal e categorias iniciais |
| POST | `/autenticacao/login` | Abre sessão |
| POST | `/autenticacao/renovar` | Troca o token de renovação por uma sessão nova |

Erros seguem `application/problem+json` (RFC 9457) com mensagens em português.

## Testes
`dotnet test`
