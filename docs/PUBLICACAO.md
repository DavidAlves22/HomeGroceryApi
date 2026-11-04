# Publicando o HomeGrocery (gratuito)

Três peças, nesta ordem: **banco (Neon)** → **API (Render)** → **app (Vercel)**. No fim, um passo volta ao Render para liberar o endereço do app.

| Peça | Serviço | Plano | Limite que importa |
|---|---|---|---|
| Banco PostgreSQL | [Neon](https://neon.com) | Free | cota mensal de computação e ~0,5–1 GB; o banco "dorme" e acorda sozinho em ~1 s |
| API .NET (Docker) | [Render](https://render.com) | Free | dorme após 15 min sem uso; a primeira chamada depois disso leva 30–60 s |
| App (PWA) | [Vercel](https://vercel.com) | Hobby | nenhum relevante |

> Confira os limites atuais nas páginas de preço de cada serviço antes de começar: planos gratuitos mudam.

Tenha à mão: conta no GitHub com os repositórios `HomeGroceryApi` e `HomeGroceryApp`.

---

## 1) Banco: Neon

1. Entre em https://neon.com e crie a conta (pode ser com o GitHub).
2. **Create project**. Nome: `homegrocery`. Versão do Postgres: a mais recente sugerida. **Região:** **AWS US East 1 (N. Virginia)**: é a mesma região que o `render.yaml` usa no Render (Virginia), então API e banco ficam perto.
3. Na tela do projeto, clique em **Connect**. Em "Connection string":
   - **Desmarque "Connection pooling"** (use a conexão direta; as migrations rodam nela).
   - Copie a URL inteira. Tem este formato: `postgresql://usuario:senha@ep-xxxx.region.aws.neon.tech/neondb?sslmode=require`
4. Guarde essa URL: ela contém a senha. **Não** cole em chat, e-mail nem commit.

A API aceita essa URL como está (converte sozinha para o formato do driver) e cria as tabelas na primeira subida.

## 2) API: Render

1. Entre em https://render.com e crie a conta (com o GitHub).
2. **New → Blueprint** → conecte o GitHub e escolha o repositório **HomeGroceryApi**. O Render lê o `render.yaml` da raiz.
3. Ele pede os valores secretos:
   - `ConnectionStrings__Padrao` → cole a URL do Neon (passo 1).
   - `Cors__OrigensPermitidas__0` → por enquanto digite `https://example.com` (você troca no passo 4).
   - `Jwt__ChaveSecreta` já é gerada pelo Render (não precisa preencher).
4. **Apply**. O primeiro build leva alguns minutos (compila a imagem Docker).
5. Quando ficar **Live**, copie a URL do serviço (algo como `https://homegrocery-api-xxxx.onrender.com`) e teste no navegador:
   `https://homegrocery-api-xxxx.onrender.com/saude` → deve mostrar `{"status":"ok"}`.
   (A primeira abertura pode demorar até ~1 minuto: o serviço estava dormindo.)

## 3) App: Vercel

1. Entre em https://vercel.com (com o GitHub) → **Add New → Project** → importe **HomeGroceryApp**.
2. Em **Framework Preset**, deixe **Other**. O `vercel.json` do repositório já define o comando de build (`npm run build:web`) e a pasta de saída (`dist`).
3. Em **Environment Variables**, adicione:
   - Nome: `EXPO_PUBLIC_API_URL`
   - Valor: a URL do Render **sem barra no final** (ex.: `https://homegrocery-api-xxxx.onrender.com`)
   - Importante: essa variável é lida **na hora do build**. Se mudar depois, é preciso fazer um novo deploy.
4. **Deploy**. Ao terminar, copie o endereço do app (ex.: `https://homegrocery-app.vercel.app`).

## 4) Liberar o app na API (CORS)

Sem isto o navegador bloqueia as chamadas do app para a API.

1. Render → serviço `homegrocery-api` → **Environment**.
2. Edite `Cors__OrigensPermitidas__0` e coloque o endereço **exato** do app na Vercel: `https://homegrocery-app.vercel.app` (com `https://`, sem barra no final).
3. Salve. O serviço reinicia sozinho (1–2 min).

## 4b) Avisos no celular (opcional, gratuito)

Para o app avisar "Ana pediu um item" e "compra finalizada" mesmo fechado, a API precisa de um par de chaves VAPID (identifica o servidor perante o serviço de push do navegador). No Render, em **Environment**, adicione:

- `Push__ChavePublica`: a chave pública (vai para o navegador)
- `Push__ChavePrivada`: a chave privada (**segredo**: só no Render, nunca no GitHub)
- `Push__Assunto`: `mailto:seu-email`

Gerar um par novo (Node): `crypto.generateKeyPairSync('ec', { namedCurve: 'prime256v1' })`, exportar como JWK; a chave privada é o campo `d` e a pública é `04 || x || y`, ambos em base64url. Sem essas variáveis os avisos ficam desligados e o botão não aparece no app (Mais > Avisos neste aparelho). Trocar o par invalida as assinaturas já feitas: cada aparelho precisa ligar os avisos de novo.

## 4c) Recuperar senha por e-mail (opcional, gratuito)

O "Esqueci minha senha" envia um código de 6 dígitos por e-mail, então a API precisa de um servidor SMTP. Um plano gratuito de provedor (ex.: Brevo, ~300 e-mails/dia) basta: crie a conta, verifique o remetente e gere a chave SMTP. No Render, em **Environment**:

- `Email__Host`: `smtp-relay.brevo.com`
- `Email__Porta`: `587`
- `Email__Usuario` e `Email__Senha`: credenciais SMTP do provedor (**segredo**: só no Render)
- `Email__De`: o remetente verificado

Sem `Email__Host` a recuperação fica desligada (`GET /autenticacao/recuperacao` devolve `disponivel: false`) e o app esconde o botão. Ainda não foi testado com um provedor real.

## 4d) Entrar com Google (opcional, gratuito)

1. No [Google Cloud Console](https://console.cloud.google.com), crie um projeto (ex.: "Home Grocery").
2. **APIs e serviços > Tela de permissão OAuth**: tipo **Externo**, preencha nome do app e e-mails. Escopos: só os básicos (e-mail, perfil, openid; não exigem verificação). Depois **publique o app** (Em produção), senão só os "usuários de teste" conseguem entrar.
3. **Credenciais > Criar credenciais > ID do cliente OAuth > Aplicativo da Web**. Em **Origens JavaScript autorizadas** coloque a URL do app na Vercel (ex.: `https://home-grocery.vercel.app`) e, para testar local, `http://localhost:8081`. **URIs de redirecionamento não são necessários.**
4. Copie o **ID do cliente** (termina em `.apps.googleusercontent.com`; é público, não é segredo) e no Render, em **Environment**, crie `Google__ClienteId` com ele (vários IDs separados por vírgula).

Sem `Google__ClienteId`, `GET /autenticacao/google` devolve `clienteId: null` e o botão não aparece no app. Quem já tem conta com o mesmo e-mail entra nela; e-mail novo cria a conta e o grupo pessoal.

## 5) Testar de ponta a ponta

1. Abra o endereço da Vercel no celular. Se o servidor estiver dormindo, aparece **"Acordando o servidor..."**: aguarde até ~1 minuto.
2. Crie sua conta, crie uma lista, marque um item.
3. **Instalar como app:**
   - Android (Chrome): menu ⋮ → **Instalar app** (ou "Adicionar à tela inicial").
   - iPhone (Safari): botão Compartilhar → **Adicionar à Tela de Início**.
4. **Família:** em *Mais → Grupo familiar*, gere um código; no outro celular (outra conta), digite o código. Marque um item em um aparelho e veja aparecer no outro.

## ATENÇÃO: o que vai em cada lugar

| Variável | Onde | Valor |
|---|---|---|
| `ConnectionStrings__Padrao` | **só no Render** | URL do Neon (`postgresql://...`). **Secreta.** |
| `EXPO_PUBLIC_API_URL` | Vercel | URL do **Render** (`https://...onrender.com`). Pública. |

**Nunca** ponha a URL do banco na Vercel. Variáveis que começam com `EXPO_PUBLIC_` são embutidas no JavaScript do site e qualquer visitante consegue lê-las. Se isso acontecer: no Neon, *Roles → Reset password*, atualize o Render com a URL nova e corrija a variável na Vercel (e faça Redeploy).

## Se algo der errado

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| App mostra "Sem conexão com o servidor" para sempre | `EXPO_PUBLIC_API_URL` errada ou ausente no build | Vercel → Settings → Environment Variables → corrija → **Redeploy** |
| `/saude` abre, mas o app não entra (erro de rede no console) | CORS: origem diferente da cadastrada | Passo 4: o valor deve ser idêntico ao endereço do app (sem barra no final) |
| `/saude` responde `banco-indisponivel` (503) | URL do Neon errada/expirada | Render → Environment → `ConnectionStrings__Padrao` (copie de novo no Neon) |
| Render "Deploy failed" / crash ao subir | Falta variável ou string de conexão inválida | Render → Logs: a mensagem diz qual |
| Muito lento só na primeira abertura | Plano gratuito dormindo | Normal. Para evitar, um serviço de "ping" a cada 10 min ou um plano pago |
| "Muitas tentativas" ao entrar | Limite de 10 tentativas/min por IP | Aguarde 1 minuto |

## Depois de publicado

- **Segredos:** ficam só no Render e no Neon. Nunca no Git.
- **Atualizar:** `git push` na branch `main` → Render e Vercel publicam sozinhos.
- **Backup:** no Neon, a restauração por histórico é curta no plano gratuito. Para algo importante, exporte periodicamente (`pg_dump` com a URL do Neon).
- **Trocar a chave de login** (se vazar): Render → Environment → gere novo valor para `Jwt__ChaveSecreta`. Todos terão de entrar de novo.

## 4e) Painel do administrador (opcional)

1. Crie a sua conta no app (ou entre com o Google) **antes** de configurar o painel.
2. No Render, em **Environment**, crie `Admin__Emails__0` com o e-mail dessa conta (mais administradores: `Admin__Emails__1`...).
3. Saia e entre de novo no app: em **Mais** aparece "Painel do administrador" (também em `/admin`). Sem a variável o painel fica fechado para todos.
4. Publique a API ANTES do app: ela aplica a migration que cria "criado em" e "último acesso" nas contas.
