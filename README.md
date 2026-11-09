# AskHR – AI Policy Assistant

An AI assistant that answers employees' HR policy questions from the company's own policy documents, with citations. Built with **.NET 10**, **Angular 22**, **Keycloak**, **SQL Server 2025 vector search** and a **local LLM via Ollama** using the RAG (retrieval-augmented generation) pattern.

> 🚧 Work in progress — milestone **M0: setup**.

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

Instructions arrive with milestone M0 (Docker Compose) and will stay under 10 steps.

> All sample policies in this repo are invented. No real company documents are used.
