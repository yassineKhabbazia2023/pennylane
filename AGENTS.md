<!-- Fichier gere par Pulse.Operations.Review (managed-files/dotnet/AGENTS.md). Ne pas le modifier dans ce repo : la pipeline de PR le remplace par la reference. -->
# AGENTS.md — Pulse backend

Instructions for coding agents (Claude Code, GitHub Copilot, Codex…). This file is identical in every Pulse backend repository. The conventions below are a copy of `conventions/dotnet.md` in `Pulse.Operations.Review`: the exact rules the AI code review applies on every PR, with the same rule IDs (`MTH-12`, `PER-01`…) as the review comments. Do not edit them here: change the source, then update the copy.

## Where to find what
| Need | Source |
|---|---|
| Coding conventions: naming, architecture, SOLID, EF Core, security, REST, events, tests | **Below in this file** (section « Conventions »). Human-readable version with examples: wiki "Conventions Backend Pulse" |
| Service role, invariants, service-specific rules | `ARCHITECTURE.md` at the repository root |
| Endpoints, published and consumed events, dependencies | `architecture.graph.yaml` at the repository root (generated, do not edit) |

## Checked automatically on every PR
Analyzers run on `dotnet build`, configured by three files at the repository root: `.editorconfig` (rule severities, naming, formatting), `pulse.analyzers.props` (analyzer packages, imported by `Directory.Build.props`) and `BannedSymbols.txt` (banned APIs). These files and this `AGENTS.md` are managed in `Pulse.Operations.Review` (`managed-files/dotnet/`) and copied by the PR pipeline: never edit them here, a local change is overwritten at the next PR. Never add a nested `.editorconfig`, a `.globalconfig`, a `<NoWarn>` or a `#pragma warning disable` to silence one of these rules: the warning is still reported on the PR. Only the team removes a rule, in the reference files.
Analyzer warnings are posted as comments on the changed lines: a change must not add any.
- Banned: `DateTime.Now` (→ `DateTime.UtcNow` or `TimeProvider`), `new HttpClient()` (→ `IHttpClientFactory`), `Environment.GetEnvironmentVariable` (→ `IOptions<T>`), `ConfigureAwait` (useless in ASP.NET Core).
- No synchronous waits (`.Result`, `.Wait()`), no `async void`. `Async` suffix, except controller actions and test methods.
- `throw;` never `throw ex;`, no `new Exception(...)`, `catch (Exception)` only at the outermost boundary (middleware, BackgroundService, consumer).
- Structured logs: constant template, PascalCase placeholders. Warning = recoverable anomaly, Error = failure requiring attention.

## How to work
- Read `ARCHITECTURE.md` first: the service's role, invariants and own rules live there, not here. One piece of information lives in one place: common conventions here, service context there.
- Check `TargetFramework` in the `.csproj` before using a recent language feature: a few services still build on `net8.0` (C# 12).
- Touch only the lines that need it: no global reformatting, no unrequested refactoring, follow the file's existing style.
- Pull requests: fill in the template (`.azuredevops/pull_request_template.md`), in French. If the AI review flags a deliberate deviation from a convention, copy the line it gives (`- API-16 : raison`) under « Écarts assumés aux conventions » with the real reason. Analyzer warnings cannot be waived this way.

## Conventions

### Project

- Stack: .NET 10, C# 14, EF Core 10, ASP.NET Core, Azure Service Bus
- Architecture: Clean Architecture (Domain → Application → Infrastructure → Web)
- Database: SQL Server (CI_AS collation — case insensitive)
- CI/CD: Azure DevOps Pipelines
- Hosting: Azure App Services, Azure Functions


### Project Structure

```
src/
  {Name}.Domain/          # Entities, enums, domain exceptions — zero dependencies
  {Name}.Application/     # Interfaces, services, DTOs, validators, mapping
  {Name}.Infrastructure/  # Repositories, DbContext, HTTP clients, event handlers
  {Name}.Api/             # Controllers, Program.cs, middleware, config
tests/
  {Name}.Application.Tests/
  {Name}.Infrastructure.Tests/
  {Name}.Api.Tests/
```

| File                      | Location                              |
|---------------------------|---------------------------------------|
| Interfaces (contracts)    | `Application/Interfaces/`             |
| Services                  | `Application/Services/`               |
| DTOs (Request/Response)   | `Application/Requests/`, `Application/Responses/` |
| Validators                | `Api/Validators/` or `Application/Validators/` |
| Mappers                   | `Application/Mappings/`               |
| Entities                  | `Domain/Entities/`                    |
| Enums                     | `Domain/Enums/`                       |
| Repositories              | `Infrastructure/Repositories/`        |
| HTTP clients              | `Infrastructure/Clients/`             |
| Event handlers            | `Infrastructure/Handlers/`            |
| DI registration           | `Application/DependencyInjection.cs`, `Infrastructure/DependencyInjection.cs` |
| DbContext                 | `Infrastructure/Data/`                |


### Project Configuration

- **API-54** `<AnalysisLevel>latest-recommended</AnalysisLevel>` in `Directory.Build.props`
- **API-55** `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`
- **API-56** `.editorconfig` at the repo root


### Naming

- **NAM-01** Private fields: `_camelCase` (e.g. `private readonly IUserService _userService;`)
- **NAM-02** Constants: `PascalCase` — never `SCREAMING_CASE`
- **NAM-03** Async methods: `Async` suffix — except controller actions
- **NAM-04** Booleans: `Is/Has/Can/Any` prefix
- **NAM-05** Interfaces: `I` prefix
- **NAM-06** No abbreviations (except Id, Xml, Ftp, Uri, Url, Http, Dto)
- **NAM-10** No Hungarian notation (`iCounter`, `strName`) — use meaningful names
- **NAM-11** Use C# aliases (`string`, `int`, `object`) not BCL names (`String`, `Int32`, `Object`)
- **NAM-12** Enum names: singular (`Status` not `Statuses`), no `Enum`/`Flag` suffix — `[Flags]` enums use plural
- **NAM-07** Method names: Verb + Resource — `CreateOrder`, `DeleteBusiness`, `GetConfiguration` — never just `Create`, `Delete`, `Get`
- **NAM-08** The parameters already indicate the lookup key — avoid redundant `ByXxx` suffixes (e.g. `GetConfiguration(int configurationId)` not `GetConfigurationById(int configurationId)`)
- **NAM-13** Code in English only (comments and commits may be in French)

#### Required Suffixes

| ID     | Type            | Suffix                   | Example                      |
|--------|-----------------|--------------------------|------------------------------|
| NAM-09 | Controller      | `*Controller`            | `SubscriptionController`     |
| NAM-14 | Service         | `*Service`               | `SubscriptionService`        |
| NAM-15 | Repository      | `*Repository`            | `SubscriptionRepository`     |
| NAM-16 | Event Handler   | `*EventHandler`          | `AccountCreatedEventHandler` |
| NAM-17 | Provider        | `*Provider`              | `NotificationProvider`       |
| NAM-18 | Entity          | `*Entity`                | `SubscriptionEntity`         |
| NAM-19 | Incoming DTO    | `*Request` or `*Command` | `CreateOrderRequest`         |
| NAM-20 | Outgoing DTO    | `*Response` or `*Dto`    | `OrderResponse`              |


### Clean Architecture — Layer Rules

Dependency direction: Web → Application → Domain, Infrastructure → Application → Domain

#### Forbidden imports

| ID     | If file is in  | NEVER use these `using`                                                                |
|--------|----------------|----------------------------------------------------------------------------------------|
| ARC-01 | DOMAIN         | `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore.*`, `System.Net.Http`, `FluentValidation`, `Microsoft.Graph`, `Newtonsoft.Json`, `Azure.*`, `Polly`, `MediatR` |
| ARC-02 | APPLICATION    | `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore.*`, `IHttpContextAccessor`, `System.Net.Http`, `Microsoft.Graph`, `Azure.Messaging`, `Microsoft.Data.*` |
| ARC-03 | INFRASTRUCTURE | `Microsoft.AspNetCore.Mvc`, `Microsoft.AspNetCore.Http` (except middleware)            |

#### Forbidden instantiations

| ID     | If file is in | NEVER `new X()` where X is                         |
|--------|---------------|-----------------------------------------------------|
| ARC-04 | APPLICATION   | Any class from Infrastructure namespace             |
| ARC-05 | WEB           | Any repository or Infrastructure class              |

#### Forbidden direct calls

| ID     | Caller      | NEVER call                             | Instead                                   |
|--------|-------------|----------------------------------------|-------------------------------------------|
| ARC-07 | WEB         | `IRepository`, `DbContext`             | Call service → service calls repository   |
| ARC-08 | APPLICATION | `HttpClient`, `GraphServiceClient`     | Call Infrastructure interface             |
| ARC-15 | DOMAIN      | Any interface                          | Domain has zero dependencies              |

#### Interfaces & DI

- **ARC-16** Interfaces defined in Application, implemented in Infrastructure
- **ARC-17** DI registration in `DependencyInjection.cs` (per layer), never in `Program.cs`


### Business Logic Placement

#### Controller (WEB) — orchestration only
- **ARC-19** No business logic in controllers — delegate to Application services
- **ARC-13** Exactly ONE service call per action (audit excepted) — chaining several service calls is orchestration and belongs in a service
- **PER-08** NEVER return an entity directly from a controller action — always map to DTO

#### Repository (INFRASTRUCTURE) — data access only
- **ARC-21** No business logic in repositories — only data persistence

#### Domain — zero dependencies
- **ARC-22** No I/O (HTTP, DB, file, message bus), no EF Core, no logging, no framework


### Validation

- **API-20** Input validation → FluentValidation `AbstractValidator<T>` on `*Request`/`*Command`


### EF Core

- **PER-01** Always `AsNoTracking()` for read-only queries
- **PER-02** Always `SaveChangesAsync()`, never `SaveChanges()`
- **PER-03** Never `.ToLower()` or `.ToUpper()` in LINQ queries — SQL Server CI_AS handles it
- **PER-19** Use `ExecuteUpdateAsync`/`ExecuteDeleteAsync` for bulk operations when no change tracking, domain events, or cascade behavior is needed
- **PER-04** Explicitly `.Include()` navigations — no lazy loading
- **PER-05** Never expose `IQueryable` outside repository — materialize with `ToListAsync()`
- **PER-20** `AsSplitQuery()` only when multiple sibling collection navigations are included — not based on Include count. Default to single query unless performance issue observed. Split queries do separate roundtrips with no consistency guarantee
- **PER-07** Prefer `AddDbContextPool` over `AddDbContext` — pooling prevents constructor injection of scoped services and custom state is not reset between uses
- **PER-21** `EnableRetryOnFailure()` for Azure SQL — EF Core default is 6 retries, do not hardcode a lower value without justification
- **PER-06** Enum properties persisted as strings: `.HasConversion<string>()` in `OnModelCreating` — `JsonStringEnumConverter` handles API serialization
- **PER-22** Wrap multiple `ExecuteUpdateAsync`/`ExecuteDeleteAsync` in an explicit transaction
- **PER-23** Never mix `SaveChangesAsync` and `ExecuteUpdateAsync`/`ExecuteDeleteAsync` on the same entities in one unit of work
- **PER-24** Anti-pattern `Detach + Update` — modify only the needed properties on the tracked entity instead
- **PER-12** NEVER query the DbContext or a repository inside a loop (N+1) — batch with a single query (`Contains`, join, or `Include`)
- **PER-25** Project with `.Select()` to the fields actually needed — do not load full entities with navigations just to map a few properties
- **PER-14** No client-side evaluation: never `.ToList()`/`.AsEnumerable()` before `Where`/`Select`/`OrderBy` — filters must translate to SQL
- **PER-13** Use `AnyAsync()` to test existence, not `CountAsync() > 0` / `Count() > 0`
- **PER-17** `ExecuteUpdateAsync`/`ExecuteDeleteAsync` do NOT refresh the change tracker and return no entity — re-reading the same rows later in the same unit of work yields stale data. Reload explicitly, or use the tracker and `SaveChangesAsync` when the entity is re-read
- **PER-18** Partial update: never load an aggregate with its `Include` navigations just to change one property — load the entity alone and assign only that property, or use `ExecuteUpdateAsync` when nothing is re-read after
- **PER-15** A list endpoint materializes with `Skip`/`Take` — an unbounded `ToListAsync()` on a growing table is a latent outage, acceptable only on a bounded reference table


### HTTP Clients

- **EVT-04** `ResiliencePipeline` / Polly policy built once (`private static readonly`) — never inside a method body
- **EVT-05** NEVER `Task.Delay` for propagation timing — use a readiness probe with Polly retry (exponential backoff + `ShouldHandle` on a typed exception)
- **HTTP-03** Prefer `AddStandardResilienceHandler()` as starting point for HTTP clients (combines rate limiter, timeout, retry, circuit breaker, attempt timeout)
- **HTTP-01** NEVER put `new HttpClient()` anywhere — use `IHttpClientFactory`
- **HTTP-04** Mandatory timeout on every external HTTP call
- **HTTP-05** Use idempotency keys for POST retries to external services


### Logging

- **API-15** Structured templates: `_logger.LogInformation("User {UserId} created", userId);`
- **API-16** NEVER string interpolation: `$"User {userId}"` — FORBIDDEN
- **API-18** NEVER concatenation: `"User " + userId` — FORBIDDEN
- **API-19** PascalCase placeholders: `{UserId}` not `{userid}`
- **API-21** Propagate CorrelationId via `X-Correlation-Id` header
- **API-17** NEVER log PII (email, phone, name, address) — log IDs only
- **API-22** NEVER log secrets (tokens, passwords, connection strings)
- **API-23** Use correct log levels: Warning = recoverable anomaly, Error = failure requiring attention — do not confuse
- **API-24** Prefer `LoggerMessage` source-generated delegates on hot paths for zero-allocation logging


### Async

- **ASY-01** Always `async/await`, never `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`
- **ASY-03** Never `async void`
- **ASY-10** Parallelize independent calls with `Task.WhenAll`
- **ASY-11** Never `Task.Run` in the HTTP request path
- **ASY-12** No fire-and-forget from controllers
- **ASY-04** Do NOT use `ConfigureAwait(false)` in application code — unnecessary in ASP.NET Core. Shared libraries consumed by UI apps may use it


### API REST

- **API-01** Plural resources: `/users`, `/subscriptions`
- **API-02** No verbs in URIs: `/users` not `/getUsers`
- **API-03** kebab-case for multi-word segments: `/booking-links`
- **API-04** POST creation → `201 Created` with `CreatedAtAction()`
- **API-25** Empty collection → `200 OK` with `[]`, not `404`
- **API-26** Errors → `ProblemDetails` (RFC 9457)
- **API-27** Mandatory pagination on GET collections (default: 20, max: 100)
- **API-06** `[ProducesResponseType]` on every controller action
- **API-28** JSON in camelCase, ignore nulls in serialization only (`WhenWritingNull`) — never ignore nulls in deserialization (explicit `null` in PATCH means deletion intent)
- **API-29** 401 = not authenticated, 403 = not authorized
- **API-05** DELETE success → `204 No Content`, not `200 OK`
- **API-32** GET with body is forbidden (RFC 9110)
- **API-33** POST for reading data is forbidden — use GET with query parameters
- **API-34** URI depth max 3 levels after `/api`: `/api/users/{id}/subscriptions` — no deeper nesting


### Security

- **API-07** Never hardcode secrets in code or config files
- **API-35** Use Managed Identity instead of connection strings with keys
- **API-08** Ownership (BOLA) is verified at the Gateway only (`RoleHandler`): microservices trust the Gateway-resolved headers — do not flag a missing ownership check in a microservice
- **API-36** Restrictive CORS (no `AllowAnyOrigin()` in production)
- **API-37** Middleware order: UseRouting → UseCors → UseAuthentication → UseAuthorization → MapControllers
- **API-10** `contactId` is ALWAYS from `CurrentUser` header (Gateway-resolved) — never from query/body
- **API-09** No PII in Service Bus events
- **API-38** Prefer `FromSql` / `FromSqlInterpolated` to ensure parameterization and avoid SQL injection. Use `FromSqlRaw` only when parameters are explicitly handled — NEVER with string interpolation or concatenation
- **API-11** NEVER use entity as `[FromBody]` parameter — over-posting / mass assignment
- **API-12** NEVER return `ex.Message` or stack trace in API response — leaks internals
- **API-39** Status/type fields MUST be enums (not `string`) — persist with `.HasConversion<string>()` in EF Core, guard transitions with methods on the entity (`MarkAsCompleted()`, `ResetForRetry()`) that throw `InvalidOperationException` on invalid transitions — never assign the property directly from outside the entity
- **API-40** No custom authentication — use ASP.NET Core authentication mechanisms
- **API-41** `FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()` — all endpoints authenticated by default, opt-out with `[AllowAnonymous]`
- **API-42** Token in query string is forbidden — always in `Authorization` header
- **API-43** BFLA: `[Authorize(Roles = ...)]` on sensitive actions (Gateway-level only)
- **API-44** Never `TypeNameHandling.All` or `TypeNameHandling.Auto` in JSON deserialization — type injection risk
- **API-45** SSRF: whitelist allowed domains when URLs come from user input
- **API-46** Path traversal: validate file paths with `Path.GetFileName()` — never use raw user input in file paths
- **API-48** `AddServerHeader = false` on Kestrel — do not expose server version
- **API-49** Headers: whitelist which headers to log — never log all request headers (may contain tokens)
- **API-50** Limit request body size (`MaxRequestBodySize`) on Kestrel/IIS
- **API-51** Rate limiting (.NET 7+) on authentication and sensitive endpoints
- **API-52** Debug endpoints (`/swagger`, `/health-details`) disabled or restricted in production
- **API-53** `UseExceptionHandler()` in non-Development environments — never expose developer exception page


### Exceptions

- **ASY-05** `throw;` to re-raise, never `throw ex;` (CA2200)
- **ASY-13** Centralized error handling: `IExceptionHandler` + `ProblemDetails` (RFC 9457) — `Pulse.ExceptionMiddleware` is legacy: keep it where it exists, NEVER add it to new code
- **ASY-06** Use typed exceptions with HTTP code — never `throw new Exception("...")`
- **ASY-07** No empty catch blocks — always log or re-throw
- **ASY-08** `catch (Exception)` only at outermost boundary (BackgroundService, middleware)
- **ASY-14** Prefer specific `catch` with `when` clause over generic `catch (Exception)` — e.g. `catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)`
- **ASY-09** `Try*` prefixed methods MUST return `bool` (with `out` for result) or `(bool Success, T Value)` tuple for async — never throw, caller decides how to handle failure. Inner non-blocking operations (e.g. link persistence) use a nested try-catch with log warning


### DateTime

- **PER-10** Never `DateTime.Now` — always UTC. In services, inject `TimeProvider` for testability; elsewhere use `DateTime.UtcNow`


### Event-Driven

- **EVT-01** Event handlers MUST be idempotent (check existence before AddAsync)
- **EVT-02** Never `catch (Exception) { return; }` in a Service Bus handler (= data loss)
- **EVT-03** Naming: `<Entity><Action>Event` (e.g. `AccountCreatedEvent`)
- **EVT-08** Never remove a field from an existing event schema (breaking change)
- **EVT-09** Distinguish domain events (in-process, same transaction) from integration events (cross-service, Service Bus)
- **EVT-11** Recommended event structure: `EventId` (Guid), `EventType`, `OccurredAt` (UTC), `CorrelationId`
- **EVT-13** One topic per service, named after the service domain in one lowercase word (`account`, `contact`, `offer`); subscription naming: `{consumer}-{topic}`


### Tests

- Naming: `MethodName_WhenCondition_ShouldExpectedResult`
- Pattern: Arrange-Act-Assert
- One test = one behavior
- Use `await` in async tests (not `.Result`)
- Centralize test data in `ModelsBuilder`
- Frameworks: xUnit, NSubstitute for new projects (Moq tolerated in existing code), FluentAssertions
- Integration tests: `WebApplicationFactory` + Testcontainers + Respawn
- `IAsyncLifetime` for async setup/teardown in xUnit
- Deterministic tests: use `FakeTimeProvider` instead of `DateTime.UtcNow` for time-dependent logic
- Don't mock what you don't own — mock your own interfaces, not framework types (e.g. mock `IUserRepository`, not `DbContext`)


### Commits

- Convention: Angular / Conventional Commits
- Format: `<type>(<scope>): <subject>`
- Types: feat, fix, refactor, perf, test, docs, style, build, ci, chore, revert
- Scope: feature within the service (e.g. `booking-link`, `account-experts`) — never a ticket number, never the API name
- Subject in imperative present tense, no initial capital, no trailing period
- Breaking changes: `!` after type (e.g. `feat(api)!: remove legacy endpoint`) or `BREAKING CHANGE:` footer
- Body (optional) explains the "why", not the "what" — the diff shows the what


### Methods & SOLID

- **MTH-01** Maximum 4 parameters per method — beyond that, group into request/command object
- **MTH-02** Maximum 4 constructor dependencies — beyond that, extract facade or service
- **MTH-10** Keep method signatures concise and readable — use request objects to reduce parameter count when line length is exceeded
- **MTH-11** Avoid multiple boolean parameters — use explicit methods or enum when ambiguous. Simple optional flags are acceptable (`bool includeDeleted = false`)
- **MTH-12** A service with too many dependencies or too many lines likely violates SRP — extract a dedicated class whose name describes its single responsibility
- **MTH-13** Magic values → named constants or enums — never hardcode strings or numbers with business meaning
- **MTH-14** Thread safety in singletons: use `ConcurrentDictionary` not `Dictionary`, `Interlocked` not `++`


### DI

- **MTH-15** Scoped for business services and repositories
- **MTH-16** Singleton for configuration and caches
- **MTH-08** NEVER inject Scoped into Singleton (captive dependency)
- **MTH-18** No `BuildServiceProvider()` in extension methods
- **MTH-19** ISP split: register concrete class once, forward each sub-interface via `sp.GetRequiredService<TImpl>()` — all share the same scoped instance
- **MTH-20** Use `IServiceScopeFactory` when a Singleton needs to consume Scoped services
- **MTH-21** Keyed Services (.NET 8+) for multiple implementations of the same interface
- **MTH-22** `IHttpContextAccessor` forbidden in Application/Domain — abstract behind `IAuthenticationContext` interface
- **MTH-23** Use `IOptions<T>` / `IOptionsMonitor<T>` for configuration — never `Environment.GetEnvironmentVariable()`
- **MTH-24** `ValidateOnStart()` on options registration for fail-fast configuration validation


### Performance

- **MTH-25** `StringBuilder` for string concatenation in loops — never `+=` in a loop
- **MTH-26** Materialize `IEnumerable` (`ToList()`) before multiple enumeration — avoid deferred execution surprises
- **MTH-27** Avoid double-check existence patterns (SELECT then INSERT) — use a single query or upsert
- **MTH-28** Minimize allocations on hot paths: prefer `Span<T>`, `ArrayPool`, stackalloc when appropriate
