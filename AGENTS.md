# HotelOps Backend

## Tech Stack

- C#
- .NET 10
- ASP.NET Core Web API
- Entity Framework Core 10
- SQL Server
- Database First

## Architecture

Current backend structure:

- The backend currently uses a single ASP.NET Core Web API project:
  `src/HotelOps.Api`

- Inside `HotelOps.Api`, organize code into folders by responsibility, such as:
  - `Controllers/`
  - `Data/`
  - `Entities/`
  - `Services/`
  - `DTOs/`
  - other folders when appropriate

- Do not create separate `Application`, `Domain`, or `Infrastructure` projects
  unless explicitly requested and agreed by the team.

- Preserve the current single-project backend structure.

- Controllers should handle HTTP concerns only.

- Business logic should be placed in services or other appropriate classes/folders
  inside `HotelOps.Api`.

- Do not perform large architectural refactors unless explicitly requested.

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

## Local Configuration and Secrets

- Local SQL Server connection strings are developer-specific.
- Do not put machine-specific SQL Server names in `appsettings.json`.
- `ConnectionStrings:HotelOpsDb` must be configured locally using .NET User Secrets.
- Never commit `secrets.json`, passwords, tokens, API keys, or local credentials.
- Do not replace the shared connection string configuration with a developer-specific value.

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


## Collaboration

This repository is shared by two backend developers.

- Treat `develop` as the shared backend integration baseline.
- Work only on the feature requested by the developer.
- Do not modify unrelated modules.
- Do not modify another developer's module unless the requested task requires it.
- Avoid changing shared database mappings, common infrastructure, or shared configuration unless necessary.
- If a requested change affects both backend developers or the shared database schema, explain the impact before implementing it.
- Do not make large cross-module refactors without explicit approval.


## Backend Module Ownership

This repository is shared by two backend developers.

Both developers work on the same ASP.NET Core backend and share the existing database, infrastructure, and project baseline.

Module ownership is organized by business features, not architectural layers.

### Backend Developer 1 (BE1)

Responsible for:

- FE-01: Authentication and Role-Based Access Control (RBAC)
- FE-02: Hotel Structure Management
- FE-03: Room Operations
- FE-04: Housekeeping
- FE-05: Inspection
- FE-11: Task Assignment
- FE-12: Work-Time Management
- FE-13: Internal Requests

### Backend Developer 2 (BE2)

Responsible for:

- FE-06: Incident and Maintenance Management
- FE-07: Asset Management
- FE-08: Inventory Management
- FE-09: Material Requests
- FE-10: Replenishment
- FE-14: Notifications and Dashboard
- FE-15: Reporting and Audit

### Shared Responsibilities

Some components may be used by both developers:

- HotelOpsDbContext and EF Core entity mappings
- Authentication and authorization infrastructure
- Shared configuration and dependency injection
- Common utilities and cross-cutting services
- Database schema and relationships
- Shared API conventions

Changes affecting shared components must be evaluated for their impact on both developers.

### Codex Task Scope

- Do not assume that the current user is always BE1 or BE2.
- Determine the developer role and target feature from the current task instructions.
- Work only within the requested feature scope.
- Read the relevant business requirements before implementing a feature.
- Do not implement another developer's modules unless explicitly requested.
- If a task requires modifications to shared components, explain the impact and request approval before making those changes.
- Do not reorganize module ownership without explicit team approval.
- When the assigned developer or feature scope is unclear, ask for clarification before modifying code.


## Workflow Before Coding

Before modifying code:

1. Understand the requested feature and its business rules.
2. Inspect the relevant controller, DbContext mappings, entities, services, DTOs, and related existing code.
3. Identify which existing database tables/entities are involved.
4. Preserve existing naming conventions and code style.
5. Do not assume missing requirements.
6. If an important business rule, status value, relationship, permission, or database mapping is unclear, ask the developer instead of inventing it.
7. Prefer the smallest change necessary to complete the requested task.
8. Avoid touching unrelated files.



## Validation

Codex must validate all changes before reporting a task as completed.

### 1. Build Verification

After modifying C# source code, Codex must run:

```bash
dotnet build HotelOps.slnx
```

- If the build succeeds, proceed with other relevant validation steps.
- If the build fails, identify the cause and fix errors related to the changes made.
- Do not declare a task complete if the new changes cause build errors.
- Do not ignore or hide build warnings.

### 2. Automated Testing

If the repository contains automated tests relevant to the modified functionality, Codex must run:

```bash
dotnet test HotelOps.slnx
```

- If tests fail, investigate and resolve failures within the scope of the task.
- If no relevant automated tests exist, explicitly state that automated tests are unavailable.
- Never claim that a feature has been fully tested unless the relevant tests have actually been executed.

### 3. Database Verification

For features that interact with SQL Server:

- Use the existing `HotelOpsDbContext`.
- Inspect the relevant EF Core entities and database mappings before writing queries.
- Ensure that the implementation is consistent with the existing database schema.
- Do not create or modify database tables, columns, foreign keys, or migrations without explicit approval.
- When practical, verify the functionality against the actual database.
- If database connectivity or query behavior has not been verified, clearly report this limitation.

### 4. API Verification

For newly created or modified API endpoints:

- Verify HTTP methods and routes.
- Verify request DTOs and response DTOs.
- Verify appropriate HTTP status codes.
- Verify input validation.
- Verify authentication and authorization where required.
- Never expose EF Core entities directly in API responses.
- When practical, execute the endpoint and verify its response.

### 5. Completion Report

After completing each task, Codex must provide a report in Vietnamese that includes:

- Features or functionality implemented.
- List of files created or modified.
- Build result: Passed / Failed.
- Test result: Passed / Failed / Not Run.
- API and database verification results, when applicable.
- Remaining errors, warnings, or known issues.
- Any functionality or behavior that could not be verified.

### 6. Mandatory Rules

- Never fabricate build or test results.
- Never hide errors or failed validation steps.
- Do not modify unrelated modules solely to make the build succeed.
- Do not automatically commit or push changes.
- If there is insufficient information to verify a feature, inform the developer.
- Clearly distinguish between "implementation completed", "build passed", and "testing passed".



## Git Workflow and Change Control

### 1. Branch Management

- `main` is the stable branch.
- `develop` is the shared integration branch for both backend developers.
- All feature development must be performed on feature branches created from `develop`.
- Follow the existing branch naming conventions, such as:
  - `feature/BE1-authentication`
  - `feature/BE1-housekeeping`
  - `feature/BE2-incident`
- Never modify `main` or `develop` directly.
- Do not create, switch, delete, or rename branches without explicit developer approval.

### 2. Git Operations

Codex must not perform the following operations without explicit developer approval:

- Create Git commits.
- Push changes to remote repositories.
- Pull, merge, or rebase branches.
- Create or merge pull requests.
- Reset, discard, or overwrite existing changes.
- Perform destructive Git operations.

Codex may inspect Git status, history, and diffs to understand the repository.

### 3. Shared Repository Rules

- Two backend developers work in this repository.
- Respect module ownership and the existing shared backend baseline.
- Never modify unrelated modules without a valid reason.
- Never overwrite or revert another developer's changes.
- Do not modify shared infrastructure or database mappings without explaining the impact.
- If a task requires cross-module changes, explain them and request approval before proceeding.

### 4. Code Changes

- Make only the changes necessary for the assigned task.
- Follow existing naming conventions and project structure.
- Do not introduce unrelated refactoring.
- Do not modify generated EF Core entities unless explicitly approved.
- Do not introduce additional projects or architectural layers without explicit approval.

### 5. Before Completing a Task

Codex must:

- Review the changes made.
- Identify all created, modified, and deleted files.
- Verify that unrelated files were not accidentally changed.
- Run the relevant build and validation steps.
- Summarize the changes in Vietnamese.
- Leave Git commit, push, and pull request operations to the developer unless explicitly instructed otherwise.



## Pending SRS Revisions and Role Clarifications

The current Software Requirements Specification (SRS) and Permission Matrix are not yet finalized.

The following changes must be considered before implementing authentication, authorization, room operations, or other role-dependent features.

### 1. Reception Role

- A new role named `Reception` will be introduced.
- This role is separated from the existing `Operation Manager` role.
- Reception is responsible for designated operations related to updating room lifecycle statuses.
- The exact permissions transferred from Operation Manager to Reception have not been finalized.
- Do not assume that Reception inherits all Operation Manager permissions.
- Do not invent room lifecycle states, transitions, or permission codes.

### 2. Department Head Role

- `Department Head` does NOT mean the manager of a single department.
- Department Head is responsible for managing departments within a hotel branch/facility.
- Authorization and data access must respect the applicable hotel branch scope.
- Do not implement Department Head as a role restricted to one department unless the revised requirements explicitly specify that behavior.

### 3. Permission Matrix Revision

- The current SRS Permission Matrix is subject to revision.
- The revised matrix must reflect the final Use Case list and updated role definitions.
- Permission assignments for Reception, Operation Manager, Department Head, and related roles must be reviewed.
- Do not treat outdated permissions as final business requirements.

### 4. Implementation Restrictions

Until the revised SRS and Permission Matrix are approved:

- Do not implement new role permissions based on assumptions.
- Do not create or change database roles, permission records, or authorization policies solely based on these pending revisions.
- Do not invent permission codes or room status transitions.
- Ask the developer for clarification when a task depends on unresolved permissions.
- Preserve the existing database schema and shared backend baseline.
- Clearly report any conflicts between the current SRS, database schema, and revised business requirements.



## Requirements Documentation and Source Priority

### 1. Documentation Sources

The project requirements are stored in:

- `docs/requirements/HotelOps_SRS_DRAFT.md`
  - The original SRS, written before the Reception role was requested.
  - Contains business flows, use cases, functional requirements, and the original Permission Matrix.
  - Must be treated as a historical draft, not the final approved specification.

- `docs/requirements/SRS_CHANGE_NOTES.md`
  - Contains requirements and clarifications introduced after the original SRS.
  - Includes the new Reception role and the clarified Department Head responsibilities.
  - Identifies unresolved requirements that must not be implemented based on assumptions.

- `src/HotelOps.Api/Data/HotelOpsDbContext.cs`
  - Contains the current EF Core database mappings.

- `src/HotelOps.Api/Entities/`
  - Contains entity classes generated from the existing SQL Server database.

### 2. Source Priority

When implementing a feature:

1. Follow the developer's explicit instructions for the current task.
2. Follow approved requirement changes when available.
3. Check `SRS_CHANGE_NOTES.md` for changes introduced after the original SRS.
4. Consult the relevant sections of `HotelOps_SRS_DRAFT.md` for existing business requirements.
5. Inspect the actual database schema, DbContext, entities, and existing code before implementing data access.

The database schema and EF Core mappings describe the current implementation, but they do not automatically override approved business requirements.

### 3. Handling Conflicts

- Do not silently resolve contradictions between documents.
- Do not assume that a newer requirement has already been approved or implemented.
- Do not treat the original Permission Matrix as final for Reception-related or Department Head-related permissions.
- If a requirement conflicts with the current database schema or existing implementation, explain the conflict before modifying code.
- Ask the developer when a required business rule is missing or unclear.

### 4. Requirement Review Before Coding

Before implementing a feature, Codex must:

1. Identify the relevant feature ID and use cases.
2. Read the relevant SRS sections and applicable Change Notes.
3. Identify unresolved or unapproved requirements.
4. Inspect the related existing implementation and database mappings.
5. Explain important ambiguities or conflicts.
6. Implement only the confirmed scope.

Do not implement pending requirements without explicit developer approval.
