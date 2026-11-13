# AskHR – AI Policy Assistant

An AI assistant that answers employees' HR policy questions from the company's own policy documents, with citations. Built with **.NET 10**, **Angular 22**, **Keycloak**, **SQL Server 2025 vector search** and a **local LLM via Ollama** using the RAG (retrieval-augmented generation) pattern.

> 🚧 Work in progress — milestones **M0: setup**, **M1: auth**, **M2: first AI call** and **M3: upload + ingestion** done; next: M4 embeddings + vector search.

## Planned features

- Secure sign-in with Keycloak; two roles: **HR Admin** and **Employee**
- HR Admin uploads PDF/text policies; a Hangfire job chunks and embeds them into SQL Server 2025 `VECTOR` columns
- Employees ask questions and get streamed answers grounded only in the policies, with sources
- Per-user chat history

## Tech stack

| Area | Technology |
| --- | --- |
| Backend | .NET 10 Web API, EF Core, Clean Architecture |
| Frontend | Angular 22 (standalone, signals, zoneless) |
| AI | Microsoft.Extensions.AI, Ollama (local chat + embedding models) |
| Vector search | SQL Server 2025 `VECTOR` + `VECTOR_DISTANCE` |
| Auth | Keycloak (OIDC, roles) |
| Jobs | Hangfire |
| Infra | Docker Compose, GitHub Actions |

## Run locally

Prerequisites: Docker Desktop (WSL 2), .NET 10 SDK, Node 24 + Angular CLI, [Ollama](https://ollama.com/download).

1. Copy `deploy/.env.example` to `deploy/.env` and set your own passwords (including the two demo users).
2. Start SQL Server 2025 and Keycloak: `docker compose -f deploy/docker-compose.yml up -d`
   On first start Keycloak imports the `askhr` realm from `deploy/keycloak/askhr-realm.json` (roles, clients, demo users).
3. Pull the models: `ollama pull llama3.2:3b` and `ollama pull nomic-embed-text`
4. Put the SQL Server connection string in user secrets (never in the repo), then create the `AskHR` database:
   `dotnet user-secrets set "ConnectionStrings:AskHr" "Server=localhost,14333;Database=AskHR;User Id=sa;Password=<SQL_SA_PASSWORD from deploy/.env>;Encrypt=True;TrustServerCertificate=True" --project backend/src/AskHR.Api`
   `dotnet tool restore` then `dotnet ef database update --project backend/src/AskHR.Infrastructure --startup-project backend/src/AskHR.Api`
5. Run the API: `dotnet run --project backend/src/AskHR.Api --launch-profile https` → https://localhost:7124/health returns `Healthy`
   (or open `backend/AskHR.slnx` in Visual Studio and run the `https` profile)
6. Run the web app: `cd frontend && npm install && npx ng serve` → http://localhost:4200 (`/api` is proxied to the API)
7. Sign in as `admin@askhr.demo` (HR Admin) or `employee@askhr.demo` (Employee) with the passwords from `deploy/.env`.

Optional API check from PowerShell (dev only, uses the `askhr-devtools` client):

```powershell
$t = ./deploy/get-token.ps1 admin        # or: employee   (add -Decode to see the claims)
curl.exe -k -H "Authorization: Bearer $t" https://localhost:7124/api/documents   # 200 admin, 403 employee

# Ask the local model (employee or admin token). The first call loads the model (~40 s on CPU), later ones take seconds.
Invoke-RestMethod -Method Post https://localhost:7124/api/chat -Headers @{ Authorization = "Bearer $t" } `
  -ContentType 'application/json' -Body '{"question":"Can I carry over unused annual leave?"}'
```

The model, URL, temperature and answer length are set in the `Ai` section of `backend/src/AskHR.Api/appsettings.json`.

Upload a policy (HR Admin) and watch it being ingested:

```powershell
$t = ./deploy/get-token.ps1 admin
curl.exe -k -H "Authorization: Bearer $t" -F "file=@docs/samples/lumora-leave-and-remote-work-policy.pdf" https://localhost:7124/api/documents
# 202 Accepted + Location: /api/documents/{id}. Poll it: status goes Processing -> Ready (3 chunks) in a few seconds.
curl.exe -k -H "Authorization: Bearer $t" https://localhost:7124/api/documents
```

The same requests are in `backend/src/AskHR.Api/AskHR.Api.http`. In Development, the Hangfire dashboard is at
`https://localhost:7124/hangfire?access_token=<admin token>` (hr-admin only; the token is then kept in a 5-minute cookie).

## How ingestion works (M3)

1. `POST /api/documents` (HR Admin, multipart, PDF or .txt up to 10 MB) checks the extension **and** the file's first bytes,
   stores the document as `Processing` with its bytes, enqueues a Hangfire job and returns **202 Accepted** at once.
2. The job (1 worker, `Ingestion` section in appsettings) extracts the text — PDF with [PdfPig](https://github.com/UglyToad/PdfPig) — and
   splits it into chunks of about **500 words with a 50-word overlap**, cut at a sentence end when one is close.
3. Chunks and the `Ready` status are saved in one transaction. Problems with the file (scanned PDF without text, password,
   damaged file) set `Failed` with a readable `failureReason`.

Not handled yet: OCR for scanned PDFs, removing repeated page headers/footers, tables and multi-column layouts.
Embeddings for each chunk come in M4.

## Repository layout

```
backend/    .NET 10 API — AskHR.slnx, src/ (Api, Application, Domain, Infrastructure), tests/
frontend/   Angular 22 app
deploy/     Docker Compose, Keycloak realm, dev token script
docs/       samples/: an invented policy (Markdown source, generated PDF, and the script that builds it)
```
`/health` reports `Degraded` (still HTTP 200) when Ollama is not running or the chat model is not pulled.

> Changed the realm file or the demo passwords? The import only runs on an empty Keycloak volume:
> `docker compose -f deploy/docker-compose.yml down keycloak` then `docker volume rm askhr_keycloak_data` and start again.

| Service | URL |
| --- | --- |
| SQL Server 2025 | `localhost,14333` (user `sa`, password from `deploy/.env`) |
| Keycloak admin | http://localhost:8080 |
| Ollama | http://localhost:11434 |

Ollama runs natively by default (lighter on 8 GB machines). To run it in Docker instead: `docker compose -f deploy/docker-compose.yml --profile ollama up -d`.

> All sample policies in this repo are invented. No real company documents are used.
