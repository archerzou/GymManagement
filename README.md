# Gym Management API

A sample **ASP.NET Core Web API** built with **Clean Architecture**, **CQRS** (via MediatR), the **Repository + Unit of Work** patterns, and **Entity Framework Core** (SQLite). It models a simple gym-management domain: an *Admin* owns a *Subscription*, a subscription contains *Gyms*, a gym contains *Rooms* and *Trainers*.

This project is intended as a **learning resource**. The sections below explain not only *how* to run it, but *why* it is structured the way it is — so a beginner can follow along step by step.

---

## Table of Contents

1. [What is Clean Architecture?](#1-what-is-clean-architecture)
2. [Solution Structure](#2-solution-structure)
3. [How a Request Flows Through the App](#3-how-a-request-flows-through-the-app)
4. [Key Patterns & Libraries](#4-key-patterns--libraries)
5. [Prerequisites](#5-prerequisites)
6. [Getting Started](#6-getting-started)
7. [Database Setup (EF Core Migrations)](#7-database-setup-ef-core-migrations)
8. [Running the API](#8-running-the-api)
9. [API Endpoints & Examples](#9-api-endpoints--examples)
10. [The Domain Rules](#10-the-domain-rules)
11. [How to Add a New Feature](#11-how-to-add-a-new-feature)
12. [Coding Conventions](#12-coding-conventions)
13. [Troubleshooting](#13-troubleshooting)

---

## 1. What is Clean Architecture?

Clean Architecture organizes code into **concentric layers**. The golden rule is the **Dependency Rule**: *source code dependencies only point inwards.* Inner layers know nothing about outer layers.

```
        ┌─────────────────────────────────────────────┐
        │              Presentation                    │   ← Web API, Contracts (DTOs)
        │   ┌─────────────────────────────────────┐    │
        │   │            Infrastructure            │    │   ← EF Core, DB, external services
        │   │   ┌─────────────────────────────┐    │    │
        │   │   │        Application          │    │    │   ← Use cases (Commands/Queries)
        │   │   │   ┌─────────────────────┐   │    │    │
        │   │   │   │      Domain         │   │    │    │   ← Entities & business rules
        │   │   │   └─────────────────────┘   │    │    │
        │   │   └─────────────────────────────┘    │    │
        │   └─────────────────────────────────────┘    │
        └─────────────────────────────────────────────┘
```

**Why bother?**

- **Testability** — business logic (Domain/Application) has no dependency on the database or web framework, so it can be unit-tested in isolation.
- **Flexibility** — you can swap SQLite for SQL Server, or the Web API for a gRPC host, without touching business rules.
- **Clarity** — each layer has one job.

See the layer diagram in [`assets/code_structure.png`](assets/code_structure.png) for a visual overview: the **Presentation** and **Infrastructure** layers sit on the outside and both depend inward on the **Application** layer, which in turn depends only on the **Domain** layer at the center.

---

## 2. Solution Structure

The code lives under `src/`, with one project per layer:

```
GymManagement/
├── GymManagement.sln
├── Directory.Build.props          # Shared build settings (Nullable, warnings-as-errors)
├── .editorconfig                  # Code style rules (enforced as build errors)
├── assets/
│   └── code_structure.png         # Clean Architecture layer diagram
└── src/
    ├── GymManagement.Domain/         # ❤️  Center — entities & business rules
    │   ├── Admins/Admin.cs
    │   ├── Gyms/        (Gym.cs, GymErrors.cs)
    │   ├── Rooms/       (Room.cs)
    │   └── Subscriptions/ (Subscription.cs, SubscriptionType.cs, SubscriptionErrors.cs)
    │
    ├── GymManagement.Application/    # 🧠  Use cases (CQRS handlers)
    │   ├── Common/Interfaces/        # Repository & UnitOfWork abstractions
    │   ├── Gyms/         (Commands/, Queries/)
    │   ├── Rooms/        (Commands/)
    │   ├── Subscriptions/(Commands/, Queries/)
    │   └── DepedencyInjection.cs     # AddApplication() — registers MediatR
    │
    ├── GymManagement.Infrastructure/ # 🔌  Implementations (EF Core, DB)
    │   ├── Common/Persistence/       # DbContext, value converters
    │   ├── Admins/Gyms/Subscriptions/Persistence/   # Repositories + EF configs
    │   ├── Migrations/               # EF Core migration history
    │   └── DepedencyInjection.cs     # AddInfrastructure() — registers DbContext & repos
    │
    ├── GymManagement.Contracts/      # 📄  Request/Response DTOs (the public API shape)
    │   ├── Gyms/ Rooms/ Subscriptions/
    │
    └── GymManagement.Api/            # 🌐  Entry point — controllers, HTTP pipeline
        ├── Controllers/
        └── Program.cs
```

### Who depends on whom

| Project | References |
|---|---|
| **Domain** | *(nothing — it is the core)* |
| **Application** | Domain |
| **Infrastructure** | Application (and transitively Domain) |
| **Contracts** | *(nothing — pure DTOs)* |
| **Api** | Application, Infrastructure, Contracts |

Notice that **Application defines interfaces** (e.g. `IGymsRepository`, `IUnitOfWork`) and **Infrastructure implements them**. This is the *Dependency Inversion Principle* in action: the inner layer owns the contract, the outer layer fulfills it.

---

## 3. How a Request Flows Through the App

Follow a `POST /subscriptions/{id}/gyms` request as an example:

```
HTTP Request
    │
    ▼
[1] GymsController.CreateGym()                 (Api layer)
    │   maps the request DTO into a Command
    ▼
[2] new CreateGymCommand(name, subscriptionId) (Application layer)
    │   sent via MediatR  →  _mediator.Send(command)
    ▼
[3] CreateGymCommandHandler.Handle()           (Application layer)
    │   - loads the Subscription via ISubscriptionsRepository
    │   - creates a Gym (Domain) and calls subscription.AddGym(gym)
    │   - persists via repositories + IUnitOfWork.CommitChangesAsync()
    ▼
[4] Subscription / Gym entities enforce rules  (Domain layer)
    │   e.g. "a Free subscription may only have 1 gym"
    ▼
[5] EF Core repositories save to SQLite        (Infrastructure layer)
    │
    ▼
[6] Handler returns ErrorOr<Gym>
    │   - on success → 201 Created + GymResponse DTO
    │   - on failure → Problem() → RFC 7807 ProblemDetails
    ▼
HTTP Response
```

The controller never talks to the database directly — it only sends a **Command** or **Query** and translates the result back into an HTTP response.

---

## 4. Key Patterns & Libraries

| Concept | Library / Mechanism | Where to look |
|---|---|---|
| **CQRS** (separate Commands vs Queries) | [MediatR](https://github.com/jbogard/MediatR) | `Application/**/Commands`, `**/Queries` |
| **Functional error handling** (no exceptions for expected errors) | [ErrorOr](https://github.com/amantinband/error-or) | every handler returns `ErrorOr<T>` |
| **Repository pattern** (abstract data access) | custom interfaces | `Application/Common/Interfaces/I*Repository.cs` |
| **Unit of Work** (one transactional commit) | `IUnitOfWork` → `DbContext` | `Infrastructure/.../GymManagementDbContext.cs` |
| **ORM / persistence** | [EF Core](https://learn.microsoft.com/ef/core/) + SQLite | `Infrastructure/.../Persistence` |
| **Guard clauses** | [Throw](https://github.com/amantinband/throw) | Domain entities |
| **API docs** | OpenAPI (`Microsoft.AspNetCore.OpenApi`) | `Program.cs` |

**CQRS in one sentence:** *Commands* change state (Create/Delete a gym) and *Queries* read state (Get/List gyms) — each gets its own small handler class, keeping logic focused and easy to test.

---

## 5. Prerequisites

Install the following before you begin:

- **[.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)** — verify with:
  ```bash
  dotnet --version      # should print 9.x.x
  ```
- **EF Core CLI tools** (for migrations) — install once globally:
  ```bash
  dotnet tool install --global dotnet-ef
  dotnet ef --version   # confirm it's installed
  ```
- An HTTP client to test endpoints: `curl`, [Postman](https://www.postman.com/), or the built-in OpenAPI UI.
- *(Optional)* An IDE: **Visual Studio 2022**, **VS Code** (+ C# Dev Kit), or **JetBrains Rider**.

---

## 6. Getting Started

```bash
# 1. Clone the repository
git clone <your-repo-url> GymManagement
cd GymManagement

# 2. Restore NuGet packages
dotnet restore

# 3. Build the whole solution
dotnet build
```

A successful build ends with `Build succeeded. 0 Warning(s) 0 Error(s)`.

> **Note:** `Directory.Build.props` sets `TreatWarningsAsErrors=true`, and `.editorconfig` promotes several style rules (for example, *"don't use `var` when the type isn't obvious"*) to **errors**. This is intentional — it keeps the codebase consistent. If the build fails on a style rule, fix the code rather than disabling the rule.

---

## 7. Database Setup (EF Core Migrations)

The app uses a local **SQLite** file named `GymManagement.db` (configured in `Infrastructure/DepedencyInjection.cs`). Migrations already exist in `Infrastructure/Migrations/`, so you only need to apply them.

EF commands must run against the **Infrastructure** project (which holds the `DbContext`) but be started from the **Api** project (which has the connection wired up):

```bash
# Apply all existing migrations and create GymManagement.db
dotnet ef database update \
  --project src/GymManagement.Infrastructure \
  --startup-project src/GymManagement.Api
```

Useful follow-up commands:

```bash
# List migrations
dotnet ef migrations list \
  --project src/GymManagement.Infrastructure \
  --startup-project src/GymManagement.Api

# Create a NEW migration after you change an entity
dotnet ef migrations add <MeaningfulName> \
  --project src/GymManagement.Infrastructure \
  --startup-project src/GymManagement.Api
```

> On **Windows PowerShell**, run each command on a single line (the `\` line-continuation is a Bash convention).

---

## 8. Running the API

```bash
dotnet run --project src/GymManagement.Api
```

By default (see `Properties/launchSettings.json`) the API listens on:

- **HTTP:**  `http://localhost:5062`
- **HTTPS:** `https://localhost:7287`

Because the app runs in the *Development* environment, the OpenAPI document is served at:

```
http://localhost:5062/openapi/v1.json
```

You can feed that URL into Swagger UI, Scalar, or Postman to explore the endpoints interactively.

---

## 9. API Endpoints & Examples

All examples use the HTTP base URL `http://localhost:5062`.

### Subscriptions

| Method | Route | Description |
|---|---|---|
| `POST` | `/subscriptions` | Create a subscription |
| `GET` | `/subscriptions/{subscriptionId}` | Get a subscription |
| `DELETE` | `/subscriptions/{subscriptionId}` | Delete a subscription |

**Create a subscription**

```bash
curl -X POST http://localhost:5062/subscriptions \
  -H "Content-Type: application/json" \
  -d '{
        "subscriptionType": "Free",
        "adminId": "11111111-1111-1111-1111-111111111111"
      }'
```

Response `201 Created`:

```json
{
  "id": "a1b2c3d4-....",
  "subscriptionType": "Free"
}
```

> `subscriptionType` accepts `"Free"`, `"Starter"`, or `"Pro"`.

### Gyms (nested under a subscription)

| Method | Route | Description |
|---|---|---|
| `POST` | `/subscriptions/{subscriptionId}/gyms` | Create a gym |
| `GET` | `/subscriptions/{subscriptionId}/gyms` | List gyms |
| `GET` | `/subscriptions/{subscriptionId}/gyms/{gymId}` | Get a gym |
| `DELETE` | `/subscriptions/{subscriptionId}/gyms/{gymId}` | Delete a gym |
| `POST` | `/subscriptions/{subscriptionId}/gyms/{gymId}/trainers` | Add a trainer to a gym |

**Create a gym**

```bash
curl -X POST http://localhost:5062/subscriptions/{subscriptionId}/gyms \
  -H "Content-Type: application/json" \
  -d '{ "name": "Downtown Gym" }'
```

### Rooms (nested under a gym)

| Method | Route | Description |
|---|---|---|
| `POST` | `/gyms/{gymId}/rooms` | Create a room |
| `DELETE` | `/gyms/{gymId}/rooms/{roomId}` | Delete a room |

**Create a room**

```bash
curl -X POST http://localhost:5062/gyms/{gymId}/rooms \
  -H "Content-Type: application/json" \
  -d '{ "name": "Yoga Studio" }'
```

### Error responses

Errors come back as standard [RFC 7807 ProblemDetails](https://datatracker.ietf.org/doc/html/rfc7807). For example, exceeding a subscription's gym limit returns `409 Conflict` with a descriptive message.

---

## 10. The Domain Rules

The business rules live in the **Domain** entities — not in controllers or the database. Subscription tier limits (`Subscription.cs`):

| Tier | Max Gyms | Max Rooms / gym | Max Daily Sessions / room |
|---|---|---|---|
| **Free** | 1 | 1 | 4 |
| **Starter** | 1 | 3 | unlimited |
| **Pro** | 3 | unlimited | unlimited |

Examples of rules enforced in code:

- `Subscription.AddGym()` rejects adding more gyms than the tier allows (`SubscriptionErrors.CannotHaveMoreGymsThanSubscriptionAllows`).
- `Gym.AddRoom()` rejects exceeding the room limit, and rejects duplicate rooms.
- `Gym.AddTrainer()` rejects adding the same trainer twice (`409 Conflict`).
- `Admin.SetSubscription()` prevents an admin from holding two subscriptions.

These rules return `ErrorOr<...>` results, which the handlers pass up to the controller, which converts them to the right HTTP status code.

---

## 11. How to Add a New Feature

Suppose you want to add **"Rename a Gym"**. Following the layers, you would:

1. **Contracts** — add a `RenameGymRequest` record in `GymManagement.Contracts/Gyms/`.
2. **Domain** — add a `Gym.Rename(string name)` method that enforces any rules (e.g. name not empty) and returns `ErrorOr<Success>`.
3. **Application** — create a `Commands/RenameGym/` folder with:
   - `RenameGymCommand` (a `record` implementing `IRequest<ErrorOr<...>>`)
   - `RenameGymCommandHandler` (loads the gym, calls `gym.Rename(...)`, saves via the repository + `IUnitOfWork`).
4. **Api** — add a `PUT` action on `GymsController` that maps the request to the command and `Send`s it via MediatR.

No change to Infrastructure is needed unless the data shape changes (then add an EF migration). This predictable, repeatable structure is the main payoff of Clean Architecture + CQRS.

---

## 12. Coding Conventions

Enforced automatically via `.editorconfig` and `Directory.Build.props`:

- **Nullable reference types** are enabled — handle `null` explicitly.
- **Warnings are treated as errors** — keep the build clean.
- **`var` only when the type is apparent.** Prefer explicit types for results of method/async calls, e.g.:
  ```csharp
  // ✅ explicit type — the type isn't obvious from the call
  ErrorOr<Gym> result = await _mediator.Send(command);

  // ❌ would fail the build (csharp_style_var_elsewhere = false:error)
  var result = await _mediator.Send(command);
  ```
- **File-scoped namespaces** and **braces on all blocks** are required.

---

## 13. Troubleshooting

| Symptom | Cause / Fix |
|---|---|
| `dotnet build` fails on a style rule (e.g. CS / IDE error about `var`) | This is the `.editorconfig` working as designed. Use an explicit type or fix the flagged code. |
| `Root element is missing` for `Directory.Build.props` | The file must contain at least `<Project>\n</Project>`. Don't leave it empty. |
| `No project was found` running `dotnet ef` | Pass both `--project src/GymManagement.Infrastructure` and `--startup-project src/GymManagement.Api`. |
| `dotnet ef` not recognized | Install the tool: `dotnet tool install --global dotnet-ef`. |
| Database errors / stale schema | Delete `GymManagement.db` and re-run `dotnet ef database update`. |
| HTTPS certificate warning on first run | Trust the dev cert: `dotnet dev-certs https --trust`. |

---

## License

This project is provided as-is for educational purposes.
