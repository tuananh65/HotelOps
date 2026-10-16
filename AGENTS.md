# HotelOps Backend

## Tech Stack

- C#
- .NET 10
- ASP.NET Core Web API
- Entity Framework Core 10
- SQL Server
- Database First

## Architecture

Target architecture:

- API
- Application
- Domain
- Infrastructure

When working with the current project structure, preserve existing conventions
and do not perform large architectural refactors unless explicitly requested.

## Database Rules

- The database schema already exists in SQL Server.
- Entity Framework Core is used with a Database First approach.
- Files generated under `Entities/` represent the existing database schema.
- Do not modify generated EF entities unless explicitly requested.
- Do not create EF migrations unless explicitly requested.
- Do not invent columns, foreign keys, relationships, enum values, or status values.
- Inspect `HotelOpsDbContext` and the relevant entities before implementing database logic.
- All database operations must be asynchronous.
- Use `CancellationToken` for asynchronous operations.
- Use `AsNoTracking()` for read-only queries where appropriate.

## API Rules

- Controllers must not contain business logic.
- Controllers are responsible for HTTP concerns only.
- Never expose EF entities directly through API responses.
- Use request and response DTOs.
- Use appropriate HTTP status codes.
- Keep API contracts independent from generated database entities.

## Business Logic

- Business rules belong in services/application layer.
- Business state transitions must be implemented in services.
- Important state changes must create audit logs where required by the existing system.
- Multi-table workflows that must succeed or fail together must use transactions.
- Do not hard-code role IDs.
- Do not hard-code branch IDs.
- Respect branch and department scope.
- Use soft deactivation where required by the existing schema/business rules.

## Scope

- Do not modify unrelated modules.
- Do not perform unrelated refactoring.
- Before implementing a feature, inspect the relevant entities, DbContext configuration,
  and existing related code.
- Prefer the smallest change necessary to complete the requested task.

## Git

- Do not commit automatically.
- Do not push automatically.
- Do not merge branches automatically.
- The developer reviews and performs Git operations manually.

## Testing

Before declaring a task complete, run:

```bash
dotnet build
dotnet test