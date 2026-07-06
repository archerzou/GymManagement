# Domain Events & Eventual Consistency in GymManagement

This document explains how the `DomainEventsQueue` mechanism works in this project — how
domain events are collected, deferred, and published to achieve **eventual consistency** — and
how the concept differs from a message queue / topic in a microservice broker such as RabbitMQ.

## The pieces involved

Your domain-events + eventual-consistency mechanism is spread across four files:

| File | Role |
|---|---|
| `Domain/Common/IDomainEvent.cs` | Marker interface — `IDomainEvent : INotification` (MediatR's publish type) |
| `Domain/Common/Entity.cs` | Base class holding `_domainEvents` list + `PopDomainEvents()` |
| `Infrastructure/.../GymManagementDbContext.cs` | Collects events on save, parks them in `HttpContext.Items["DomainEventsQueue"]` |
| `Infrastructure/.../EventualConsistencyMiddleware.cs` | Drains the queue *after the response is sent*, publishes each event, commits the transaction |

## How a domain event travels, step by step

Trace a `DELETE` that ends up calling `Admin.DeleteSubscription(...)`:

**1. The entity records intent, not action** (`Admin.cs:37`)
```csharp
_domainEvents.Add(new SubscriptionDeletedEvent(subscriptionId));
```
The `Admin` mutates its own state (`SubscriptionId = null`) and *appends a fact* — "a subscription was deleted" — to its in-memory `_domainEvents` list. It does **not** delete gyms or the subscription itself. The domain stays ignorant of side effects.

**2. Middleware opens a transaction up front** (`EventualConsistencyMiddleware.cs:15`)
Because the middleware runs before `_next(context)`, the DB transaction wraps the *entire* request, including everything that happens later.

**3. Saving harvests the events** (`GymManagementDbContext.cs:22-31`)
When the command handler calls `CommitChangesAsync()`, the DbContext scans every tracked `Entity`, calls `PopDomainEvents()` (which returns a copy and **clears** the list so events aren't re-fired), and enqueues them:
```csharp
_httpContextAccessor.HttpContext!.Items["DomainEventsQueue"] = domainEventsQueue;
```
The events are now stashed on the per-request `HttpContext.Items` bag. Then `SaveChangesAsync()` writes — but nothing is *committed* yet (open transaction).

**4. The response is sent to the client — then events fire** (`EventualConsistencyMiddleware.cs:17-30`)
`context.Response.OnCompleted(...)` runs *after* the HTTP response has been flushed to the client. Only now does it drain the queue:
```csharp
while (domainEventsQueue.TryDequeue(out IDomainEvent? domainEvent))
{
    await publisher.Publish(domainEvent);   // MediatR fan-out
}
await transaction.CommitAsync();
```

**5. Handlers react** — `MediatR.Publish` fans the one event out to **both** `INotificationHandler<SubscriptionDeletedEvent>` implementations:
- `Gyms/Events/SubscriptionDeletedEventHandler` → removes all gyms of that subscription
- `Subscriptions/Events/SubscriptionDeletedEventHandler` → removes the subscription itself

Each handler calls `CommitChangesAsync()` again — which can enqueue *new* domain events into the same queue. That's exactly why a **`Queue` drained by a `while` loop** is used: it supports **cascading events** (event → handler → new event → handler…) until the queue empties.

## Why this is "eventual consistency"

From the client's point of view, the response comes back **before** the gyms and subscription are actually deleted. There is a window where:
- the `Admin` says "I have no subscription" (already returned to the client), but
- the gyms/subscription rows still exist and get cleaned up moments later, in `OnCompleted`.

The system is *temporarily inconsistent* and becomes *consistent eventually*. The comment at `EventualConsistencyMiddleware.cs:34` names the tradeoff: if event processing throws, the client already received a success response, yet the transaction is never committed — so the primary change silently rolls back too. (Note: because the whole request shares one transaction that only commits at the very end, this is actually atomic *at the database level* — the "eventual" part is about **timing/observability within the request**, not about partial DB writes.)

## The mental model

Domain events here are a way to **decouple a state change from its side effects inside one process**:
- The `Admin` aggregate doesn't know gyms exist.
- The gym-cleanup logic doesn't live in the delete-subscription command.
- They communicate through an event, wired by MediatR.

---

## Difference vs. a message queue / topic (RabbitMQ)

The key thing to understand: **your `Queue<IDomainEvent>` is not the analog of a RabbitMQ queue.** It's just an in-memory buffer to *defer* processing to the end of the request. The actual pub/sub fan-out is done by `MediatR.Publish`. So the closest analogy is:

- `MediatR.Publish` (one event → many handlers) ≈ a **topic / fanout exchange**
- `MediatR.Send` (one command → exactly one handler) ≈ a **queue / point-to-point**

But even that analogy is loose, because everything here is **in-process**. Here's the real comparison:

| Aspect | Your domain events (in-process) | RabbitMQ queue/topic (out-of-process) |
|---|---|---|
| **Scope** | Single app, single deployable | Across processes / microservices / machines |
| **Transport** | `Queue<T>` in `HttpContext.Items` + method calls | Network protocol (AMQP), a broker in the middle |
| **Timing** | Same request lifecycle (after response, before commit) | Fully asynchronous; consumer may process seconds/hours later |
| **Durability** | In RAM — **lost if the process crashes** mid-request | Persisted to disk; survives restarts |
| **Delivery guarantee** | Best-effort, at-most-once; a throw aborts the batch | At-least-once with acks, redelivery, dead-letter queues |
| **Transaction** | Shares the app's DB transaction (all-or-nothing) | Separate transactions per service; no shared DB tx |
| **Coupling** | Compile-time; handlers reference the same types | Decoupled; producer/consumer deploy independently |
| **Failure handling** | `try/catch` in middleware, no retry | Retry policies, DLQ, poison-message handling |
| **Ordering / scaling** | In-order, single-threaded drain | Configurable; competing consumers scale horizontally |

### Queue vs. Topic in RabbitMQ specifically

- **Queue (work queue / point-to-point):** a message is delivered to **exactly one** consumer among those competing on that queue. Used for distributing *work* (e.g., "resize this image"). Analogous to a **command** — `MediatR.Send`.
- **Topic / exchange (publish-subscribe):** a producer publishes once to an *exchange*, which **fans the message out** to every bound queue (each subscriber gets its own copy). A "topic exchange" additionally routes by routing-key patterns (`order.created`, `order.*`). Analogous to an **event** — `MediatR.Publish`, which is precisely why your single `SubscriptionDeletedEvent` reaches *two* independent handlers.

### The conceptual bottom line

Both patterns implement the same idea — *"announce that something happened; let interested parties react"* — but at different boundaries:

- **Domain events (your code):** eventual consistency **within a single process and a single database transaction**. Great for keeping aggregates decoupled and side effects out of your command handlers. If the process dies, the work simply never happened (and the transaction rolls back), so nothing is half-done.
- **Message queues/topics (RabbitMQ):** eventual consistency **across service and database boundaries**. The event outlives your process, crosses the network, and is guaranteed to be delivered and retried — at the cost of no shared transaction (you need patterns like the **Transactional Outbox** to avoid losing events, and idempotent consumers to tolerate duplicate delivery).

A common real-world evolution: teams start with exactly what you have (in-process MediatR domain events), and when a side effect needs to move to another microservice, they replace the in-memory `Publish` with an **outbox → RabbitMQ/Kafka** hop — the domain code raising the event stays the same; only the *transport* changes.

> **Caveat in the current implementation** (observation, not a required change): because the queue lives in RAM and is drained in `OnCompleted`, a crash between "response sent" and "transaction committed" loses the side effects entirely — this is the classic gap that a durable **outbox** closes if you ever need stronger guarantees.
