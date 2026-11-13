# AskHR – AI Policy Assistant

An AI assistant that answers employees' HR policy questions from the company's own policy documents, with citations. Built with **.NET 10**, **Angular 22**, **Keycloak**, **SQL Server 2025 vector search** and a **local LLM via Ollama** using the RAG (retrieval-augmented generation) pattern.

> 🚧 Work in progress — milestones **M0: setup**, **M1: auth** and **M2: first AI call** done; next: M3 upload + ingestion.

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
4. Run the API: `dotnet run --project src/AskHR.Api --launch-profile https` → https://localhost:7124/health returns `Healthy`
5. Run the web app: `cd src/askhr-web && npm install && npx ng serve` → http://localhost:4200 (`/api` is proxied to the API)
6. Sign in as `admin@askhr.demo` (HR Admin) or `employee@askhr.demo` (Employee) with the passwords from `deploy/.env`.

Optional API check from PowerShell (dev only, uses the `askhr-devtools` client):

```powershell
$t = ./deploy/get-token.ps1 admin        # or: employee   (add -Decode to see the claims)
curl.exe -k -H "Authorization: Bearer $t" https://localhost:7124/api/documents   # 200 admin, 403 employee

# Ask the local model (employee or admin token). The first call loads the model (~40 s on CPU), later ones take seconds.
Invoke-RestMethod -Method Post https://localhost:7124/api/chat -Headers @{ Authorization = "Bearer $t" } `
  -ContentType 'application/json' -Body '{"question":"Can I carry over unused annual leave?"}'
```

The model, URL, temperature and answer length are set in the `Ai` section of `src/AskHR.Api/appsettings.json`.
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
