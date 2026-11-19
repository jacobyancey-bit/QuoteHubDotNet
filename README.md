# QuoteHubDotNet

[![CI/CD - Test, Publish & Deploy](https://github.com/jacobyancey-bit/QuoteHubDotNet/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/jacobyancey-bit/QuoteHubDotNet/actions/workflows/ci-cd.yml)

A production-grade **ASP.NET Core 10** REST API engineered for high-throughput daily inspirational quotes, drop-in Discord and Slack webhook embeds, dynamic SVG vector quote cards, community upvotes, and multi-cloud deployment.

## 🌐 Live Deployments & Docs

- **Interactive Scalar Docs (Railway + PostgreSQL):** https://quotehub-api-production.up.railway.app
- **Azure Container Apps:** https://quotehub-api.salmonbay-0fab5fd5.eastus.azurecontainerapps.io/
- **Live SVG Vector Card Demo:** https://quotehub-api-production.up.railway.app/api/v1/quotes/daily/card.svg

## 🏛️ Architecture & Clean Design

```text
HTTP Request
   ↓
Middleware Pipeline         →  GlobalExceptionHandler (ProblemDetails), Tiered RateLimiter, OutputCache, JWT Auth, OpenTelemetry
   ↓
API / Presentation Layer    →  Controllers/QuotesController.cs, Controllers/AuthController.cs, Dtos/QuoteDtos.cs
   ↓
Service / Business Layer    →  Services/IQuoteService.cs, EfQuoteService.cs, JwtTokenService.cs, SoftDeleteCleanupService.cs, CardGenerator.cs
   ↓
Domain / Model Layer        →  Models/Quote.cs, Models/Tag.cs (Many-to-Many)
   ↓
Data Access Layer           →  Data/QuoteDbContext.cs (EF Core 10: PostgreSQL in cloud, SQLite locally)
```

## ⚡ Key Enterprise & Commercial Features

- **Bot-Ready Discord Webhooks:** `GET /api/v1/quotes/daily/discord` generates drop-in Discord Rich Embeds with custom coloring and authors.
- **Dynamic Vector SVG Cards:** `GET /api/v1/quotes/daily/card.svg` renders stylized image cards with light/dark theme support.
- **Community Upvoting & Top Leaderboards:** `POST /api/v1/quotes/{id}/like` and `GET /api/v1/quotes/top?limit=10`.
- **JWT Auth & Role-Based Access Control (RBAC):** Token issuance (`POST /api/v1/auth/token`) with public `GET` endpoints and `[Authorize(Roles = "Admin")]` on `POST`/`PUT`/`DELETE`.
- **EF Core 10 & Relationships:** Many-to-many `Quote ↔ Tag` join table, connection resiliency retry policies (`EnableRetryOnFailure`), and a Global Query Filter (`HasQueryFilter(q => !q.IsDeleted)`).
- **Tiered Rate Limiter:** Free/anonymous traffic gets 100 req/min; RapidAPI / authenticated callers get 500 req/min.
- **Output Caching:** 30-second RAM cache on list endpoints with instant eviction on write actions.
- **Background Worker (`BackgroundService`):** `SoftDeleteCleanupService` periodically purges soft-deleted quotes older than 30 days using `IServiceScopeFactory`.
- **Observability & Error Handling:** Deep `/health` checks probing database connectivity, OpenTelemetry distributed tracing (OTLP), structured `ILogger<T>`, and RFC 7807 `ProblemDetails`.
- **Comprehensive Test Suite:** 28 automated xUnit tests (Unit + In-memory SQLite + HTTP integration tests with `WebApplicationFactory`).
- **Multi-Cloud CI/CD:** Multi-stage .NET 10 `Dockerfile`, GitHub Actions CI/CD publishing to `ghcr.io`, auto-deploying to Railway and Azure Container Apps, with AWS ECS/App Runner specifications.

## 🛠️ Endpoints Overview

| Method | Route | Description |
| :--- | :--- | :--- |
| `GET` | `/api/v1/quotes/daily` | Deterministic Quote of the Day (rotates at 00:00 UTC) |
| `GET` | `/api/v1/quotes/daily/discord` | Ready-to-post Discord Webhook Rich Embed JSON |
| `GET` | `/api/v1/quotes/daily/card.svg` | Dynamic SVG vector image card (`?theme=dark` or `light`) |
| `GET` | `/api/v1/quotes/top` | Top-voted quotes leaderboard |
| `POST` | `/api/v1/quotes/{id}/like` | Upvote a quote |
| `GET` | `/api/v1/quotes` | Paginated quotes list with optional `category` and `tag` filters |
| `GET` | `/api/v1/quotes/search?q=` | Case-insensitive search across quote text & author |
| `POST` | `/api/v1/quotes` | **[Admin]** Create a quote with tags (`201 Created`) |
| `PUT` | `/api/v1/quotes/{id}` | **[Admin]** Update an existing quote |
| `DELETE` | `/api/v1/quotes/{id}` | **[Admin]** Soft-delete a quote (`204 No Content`) |
| `POST` | `/api/v1/auth/token` | Obtain JWT Bearer access token |
| `GET` | `/health` | Deep health probe verifying container & DB connectivity |

## 🧪 Running Locally & Testing

```powershell
# Run the 28-test automated test suite
dotnet test

# Run the API locally (SQLite database auto-created)
dotnet run --project QuoteHub.Api
```
Visit `http://localhost:5254/scalar/v1` for the interactive OpenAPI documentation.
