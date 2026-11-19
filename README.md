# QuoteHubDotNet

[![CI/CD - Test, Publish & Deploy](https://github.com/jacobyancey-bit/QuoteHubDotNet/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/jacobyancey-bit/QuoteHubDotNet/actions/workflows/ci-cd.yml)

A production-style **ASP.NET Core 10** REST API demonstrating layered architecture, DTOs, Dependency Injection, JWT Role-Based Auth, Entity Framework Core (PostgreSQL + SQLite) with many-to-many Tags, built-in Rate Limiting & Output Caching, Background Workers, OpenTelemetry tracing, Scalar OpenAPI docs, Docker, GitHub Actions CI/CD, and multi-cloud deployment.

## Live Deployments

- **Azure Container Apps (Scalar UI):** https://quotehub-api.salmonbay-0fab5fd5.eastus.azurecontainerapps.io/
- **Railway + Managed PostgreSQL (Scalar UI):** https://quotehub-api-production.up.railway.app

## Architecture

```text
HTTP Request
   ↓
Middleware Pipeline         →  GlobalExceptionHandler (ProblemDetails), RateLimiter, OutputCache, JWT Auth, OpenTelemetry
   ↓
API / Presentation Layer    →  Controllers/QuotesController.cs, Controllers/AuthController.cs, Dtos/QuoteDtos.cs
   ↓
Service / Business Layer    →  Services/IQuoteService.cs, EfQuoteService.cs, JwtTokenService.cs, SoftDeleteCleanupService.cs
   ↓
Domain / Model Layer        →  Models/Quote.cs, Models/Tag.cs (Many-to-Many)
   ↓
Data Access Layer           →  Data/QuoteDbContext.cs (EF Core 10: PostgreSQL in cloud, SQLite locally)
```

## Key Enterprise Features

- **JWT Auth & RBAC:** HS256 JWT Bearer authentication (`POST /api/v1/auth/token`) with public `GET` endpoints and `[Authorize(Roles = "Admin")]` on `POST`/`PUT`/`DELETE`.
- **EF Core 10 & Relationships:** Many-to-many `Quote ↔ Tag` join table, Fluent API indexes, `AsNoTracking()` SQL projections, and a Global Query Filter (`HasQueryFilter(q => !q.IsDeleted)`) for soft deletes.
- **Built-in Rate Limiting & Output Caching:** Fixed-window IP rate limiting (`429 Too Many Requests`) and 30-second Output Caching on `GET /api/v1/quotes` with automatic tag eviction on writes.
- **Background Worker (`BackgroundService`):** `SoftDeleteCleanupService` periodically purges soft-deleted quotes older than 30 days using `IServiceScopeFactory`.
- **Observability & Error Handling:** OpenTelemetry distributed tracing (OTLP), structured `ILogger<T>`, RFC 7807 `ProblemDetails` global exception handler, and `/health` endpoint.
- **API Versioning & Scalar Docs:** Versioned `/api/v1/quotes` routes (with `/api/quotes` compatibility alias) and interactive Scalar OpenAPI UI at `/scalar/v1`.
- **Three-Tier Automated Tests:** xUnit unit tests, real in-memory SQLite EF Core tests, and full HTTP integration tests via `WebApplicationFactory<Program>`.
- **CI/CD & Containers:** Multi-stage .NET 10 `Dockerfile` + GitHub Actions workflow running `dotnet test`, publishing to `ghcr.io`, and supporting Azure Container Apps auto-deploy.

## Getting Started

```powershell
# Run the full test suite (Unit + EF Core SQLite + HTTP Integration)
dotnet test

# Run the API locally (SQLite)
dotnet run --project QuoteHub.Api
```

- **Local Scalar Docs:** `http://localhost:5254/scalar/v1`
- **Health Check:** `http://localhost:5254/health`

## Endpoints

| Method | Route | Auth | Description |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/auth/token` | Public | Issue a JWT Bearer token (`Admin` or `Reader`) |
| `GET` | `/api/v1/quotes?category=&tag=&page=1&pageSize=20` | Public | Paginated, output-cached list of quotes |
| `GET` | `/api/v1/quotes/{id}` | Public | Get a quote by ID (includes tags) |
| `GET` | `/api/v1/quotes/random` | Public | Get a random active quote |
| `GET` | `/api/v1/quotes/search?q=hopper` | Public | Case-insensitive search across text & author |
| `POST` | `/api/v1/quotes` | **Admin** | Create a quote with optional tags (`201 Created`) |
| `PUT` | `/api/v1/quotes/{id}` | **Admin** | Update an existing quote and its tags |
| `DELETE` | `/api/v1/quotes/{id}` | **Admin** | Soft-delete a quote (`204 No Content`) |
| `GET` | `/health` | Public | Service health check |
