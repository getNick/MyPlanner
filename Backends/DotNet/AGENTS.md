# MyPlanner .NET Backend

This is the backend service for the MyPlanner application, built with .NET 10. It provides a RESTful API for managing pages, todo lists, tasks, and notes, with support for user-based sharing and multi-level page hierarchies.

## Project Overview

- **Architecture:** Layered Architecture (API, Service, Data, Contracts).
- **Primary Technologies:**
  - **Framework:** .NET 10 (ASP.NET Core)
  - **Database:** MySQL via Entity Framework Core (Pomelo provider)
  - **Authentication:** JWT Bearer with OpenID Connect (OIDC)
  - **API Documentation:** OpenAPI with Scalar API Reference
  - **Testing:** NUnit v4+, Moq, EF Core InMemory provider, and Microsoft.NET.Test.Sdk
- **Key Patterns:**
  - **Unit of Work & Repository:** Managed in the `MyPlanner.Data` project.
  - **Dependency Injection:** Centralized in `MyPlanner.API/Program.cs`.
  - **Global Exception Handling:** Custom middleware in `GlobalExceptionHandler.cs`.
  - **Mapping:** Manual mapping between entities and contracts/models.

## Project Structure

- **MyPlanner.API:** The entry point. Contains controllers, middleware, and API-specific models.
- **MyPlanner.API.Contracts:** Shared data contracts for the API.
- **MyPlanner.Service:** Business logic layer. Contains service implementations and business models.
- **MyPlanner.Data:** Data access layer. Includes `ApplicationDbContext`, EF Core entities, migrations, and the Unit of Work/Repository implementation.
- **MyPlanner.UnitTests:** NUnit test project for verifying service logic.

## Building and Running

### Prerequisites

- .NET 10 SDK
- MySQL Server

### Configuration

The application requires a MySQL connection string. It looks for an environment variable named `MYSQLCONNSTR_LOCALDB`.

### Commands

- **Build:** `dotnet build`
- **Run API:** `dotnet run --project MyPlanner.API`
- **Run Tests:** `dotnet test`

## Development Conventions

### Coding Style

- **Top-level statements:** Used in `Program.cs`.
- **Async/Await:** All I/O bound operations (database, API calls) should be asynchronous.
- **Nullability:** Enabled (`<Nullable>enable</Nullable>`). Use nullable reference types where appropriate.
- **Dependency Injection:** Use constructor injection for services and repositories.

### Data Access

- Use the `IUnitOfWork` to interact with the database within services.
- Migrations are managed within the `MyPlanner.Data` project.

### Testing

- Place unit tests in the `MyPlanner.UnitTests` project.
- Use EF Core InMemory provider for data access — never mock `IUnitOfWork` or repositories even if they exist.
- Mock only external dependencies (HTTP clients, file systems, third-party APIs).
- Follow the Arrange-Act-Assert (AAA) pattern.
- **Refer to `testing-plan.md` for detailed testing strategies, critical gaps to address, and specific implementation patterns (e.g., using `TimeProvider`).**

### API

- Controllers should inherit from `ControllerBase` and use the `[ApiController]` attribute.
- Use `CreatedAtAction`, `Ok`, `NotFound`, and `BadRequest` for consistent HTTP responses.
- Protect endpoints with `[Authorize]`.
