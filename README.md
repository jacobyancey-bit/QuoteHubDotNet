# QuoteHubDotNet

A production-style **ASP.NET Core 10** REST API demonstrating layered architecture, DTOs, Dependency Injection, Entity Framework Core with SQLite migrations, global RFC 7807 `ProblemDetails` exception handling, structured logging, pagination, Scalar OpenAPI docs, and automated unit + HTTP integration testing.

## Architecture

```text
HTTP Request
   ↓
API / Presentation Layer    →  Controllers/QuotesController.cs, Dtos/QuoteDtos.cs, Middleware/GlobalExceptionHandler.cs
   ↓
Service / Business Layer    →  Services/IQuoteService.cs, Services/EfQuoteService.cs
   ↓
Domain / Model Layer        →  Models/Quote.cs
   ↓
Data Access Layer           →  Data/QuoteDbContext.cs (EF Core 10 + SQLite Migrations)
```

## Key Features

- **Layered Architecture & DI:** Thin controller depending on `IQuoteService` (`AddScoped`), decoupled from EF Core storage.
- **DTOs & Validation:** Immutable C# `record` DTOs (`QuoteResponse`, `CreateQuoteRequest`, `UpdateQuoteRequest`, `PagedResult<T>`) with automatic Data Annotations validation (`400 Bad Request` `ProblemDetails`).
- **EF Core 10 + Migrations:** Fluent API configuration, indexed `Category`, code-first migrations, data seeding, `AsNoTracking()` read projections, and a Global Query Filter (`HasQueryFilter(q => !q.IsDeleted)`) for soft deletes.
- **Async All The Way Down:** Full `async`/`await` + `CancellationToken` propagation from HTTP requests down to SQL queries.
- **Observability & Resilience:** `IExceptionHandler` global error handling (RFC 7807 `ProblemDetails`), structured `ILogger<T>` logging, and `/health` endpoint.
- **Interactive Docs:** Built-in `Microsoft.AspNetCore.OpenApi` + **Scalar** UI at `/scalar/v1`.
- **Automated Test Suite:** xUnit unit tests, in-memory SQLite EF Core tests, and full HTTP integration tests via `WebApplicationFactory<Program>`.

## Getting Started

```powershell
# Restore local tools (dotnet-ef) and apply database migrations
dotnet tool restore
dotnet tool run dotnet-ef database update --project QuoteHub.Api

# Run the API
dotnet run --project QuoteHub.Api
```

- **Scalar API Docs:** `http://localhost:5254/scalar/v1`
- **Health Check:** `http://localhost:5254/health`
- **Run All Tests:** `dotnet test`

## Endpoints

| Method | Route | Description |
| :--- | :--- | :--- |
| `GET` | `/api/quotes?category=&page=1&pageSize=20` | Paginated list of quotes with optional category filter |
| `GET` | `/api/quotes/{id}` | Get a quote by ID |
| `GET` | `/api/quotes/random` | Get a random active quote |
| `GET` | `/api/quotes/search?q=hopper` | Case-insensitive search across quote text & author |
| `POST` | `/api/quotes` | Create a new quote (`201 Created` + `Location` header) |
| `PUT` | `/api/quotes/{id}` | Update an existing quote |
| `DELETE` | `/api/quotes/{id}` | Soft-delete a quote (`204 No Content`) |
| `GET` | `/health` | Service health check |
