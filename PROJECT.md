# PROJECT.md — SLSEA National Solar Generation API

**Module:** Web API Development (`COBSCCCOMP251P`)
**Student Index:** `COBSCCCOMP251P-040`
**Repository:** https://github.com/JMNSJ/Web-Api-Cobsccomp251p-040
**Stack:** ASP.NET Core Web API · C# · EF Core · SQL Server · JWT · Swagger
**Document status:** Living document — this is the working plan, `README.md` is the polished public face.

---

## 1. The Problem

The Sri Lanka Sustainable Energy Authority (SLSEA), under the Ministry of Energy, needs a national
system to acquire and serve real-time and historical power-generation data from solar-powered
households. The API at the centre of this system must:

- **Acquire** readings pushed automatically by metering devices at rooftop solar sites.
- **Serve** those readings to two families of downstream consumers — operational dashboards ("what is
  generating now?") and analytical BI systems ("how has generation behaved over time, by region?").
- **Enforce** that the party producing data (the device) and the party consuming it (the SLSEA user)
  are entirely separate, with different permissions.

We deliver **the API, its documentation surface, and the reasoning behind it.** No dashboard, BI tool,
or client application is built. Downstream consumers are named only to justify why the read path must
be rich and correct.

> **Transfer note.** This is a new domain, not the taught reference system redeployed. The architecture
> is derived fresh from the model in §3, and every design decision below is recorded alongside its
> rationale in the **Design Decisions Register** in §13.

---

## 2. The Two-Client Split (this drives the entire security model)

| Client | Role | Can do | Cannot do |
|--------|------|--------|-----------|
| **Metering device** | Write-client | Authenticate *as its own installation*; push generation readings *for that installation only* | Read anything, write anything else, act as another installation |
| **SLSEA user** | Read-client | Read data scoped to their jurisdiction (national / provincial / district) | Write generation readings |

This is explicitly **not** a "person logs their own solar output" system. The producer and the consumer
are different parties. Every authorization decision in this project flows from this table.

---

## 3. Domain Model

Six entities. Five form a geographic-and-asset hierarchy; the sixth is the actor.

```
Province (1) ──< District (1) ──< GridSubstation (1) ──< SolarInstallation (1) ──< GenerationReading
                                                                    │
User ──(role + jurisdiction scope)──────────────────────────────────┘
```

| Entity | Role | Relationship |
|--------|------|--------------|
| `Province` | Top-level jurisdiction scope | 1 Province has many Districts |
| `District` | Mid-level jurisdiction scope | 1 District has many GridSubstations |
| `GridSubstation` | The grid node installations connect to | 1 Substation has many Installations |
| `SolarInstallation` | A rooftop solar site (the asset) | 1 Installation has many Readings |
| `GenerationReading` | A single timestamped generation record | Append-only time series |
| `User` | An SLSEA person with a role and a jurisdiction | Belongs to exactly one scope |

### 3.1 Two core modelling decisions

**Decision A — the meter identifier is an attribute, not an entity.**
`SolarInstallation` carries `MeterId` (and optionally `InverterId`) directly. There is **no `Device`
entity**. A device is not an independent thing with its own lifecycle here — it *is* the installation's
reporting endpoint. Introducing a needless `Device` entity is a modelling flaw: it adds a join, an
identity, and a table for zero new domain meaning.

**Decision B — `GenerationReading` is its own append-only time series.**
We do **not** store `LastPower` / `LastEnergy` fields on `SolarInstallation`. That is the single most
common mistake in this problem and it destroys the analytical capability outright — you cannot answer
"how has generation behaved over time" from a last-value column. Historical readings are the product.

### 3.2 Required fields on `GenerationReading`

| Field | Type | Why |
|-------|------|-----|
| `Id` | `Guid` or `long` | Surrogate key |
| `SolarInstallationId` | FK | The owning installation |
| `Timestamp` | `DateTimeOffset` (UTC) | When the reading was taken; indexed, drives sort/filter |
| `PowerKw` | `decimal` | Instantaneous power (kW) — the operational figure |
| `EnergyKwh` | `decimal` | Cumulative energy (kWh) — the analytical figure |
| `Voltage` | `decimal` | Voltage (V) — asset health / plausibility check |

**Additional fields we add, and the justification:**

| Field | Why we add it |
|-------|---------------|
| `IngestedAt` | Server receipt time. Separates *when it happened* from *when we learned about it*. Essential for debugging late/out-of-order device pushes. |
| `SequenceNo` | Monotonic per installation. Detects gaps and duplicates from device retries — devices *will* retransmit. |

> **Why `Timestamp` is not the primary key:** the same device can, on retry, report the same timestamp
> twice. Keying on timestamp would collapse legitimate retransmits and block idempotency handling.
> Instead, `(SolarInstallationId, Timestamp)` gets a **unique index** and `Id` remains the surrogate key.

---

## 4. Seed Data

Must be plausible and foreign-key consistent, so every endpoint returns real data.

| Element | Scale | Purpose |
|---------|-------|---------|
| Provinces | 9 | Top-level jurisdiction (all 9 Sri Lankan provinces) |
| Districts | 25 | Mid-level jurisdiction scope |
| Grid Substations | 20+ | Grid-node grouping |
| Solar Installations | 200+ | The metered assets (write clients) |
| Generation Readings | 1+ week per installation | Append-only history; pagination must matter |

**Reading generation:** one reading every **15 minutes** for **7 days** = 672 readings per installation.
Across 200+ installations that is **~134,000 rows** — enough that pagination, filtering, and sorting are
genuinely necessary rather than decorative.

**Realistic diurnal shape:** power rises from ~06:00, peaks midday, falls to zero by ~18:30, zero
falls to zero by ~18:30, zero overnight. Model it roughly as a sine/bell curve on hour-of-day, scaled by
a per-installation capacity, with small random noise. `EnergyKwh` accumulates monotonically and resets
at midnight. `Voltage` sits ~230V ± small variation.

> Seeding ~134k rows must be done in **bulk** (`AddRange` in batches, or raw `SqlBulkCopy`), not
> row-by-row `SaveChanges()` per reading — otherwise seeding takes minutes.

---

## 5. Required API Surface

Each capability names the design area it exercises. **This is the implementation checklist.**

### Read path — hierarchy and assets

| # | Capability | Design area | Shape |
|---|-----------|-------------|-------|
| R1 | Collection + atomic resources for `provinces`, `districts`, `gridsubstations`, `installations` | Resource naming & nesting | `/api/provinces`, `/api/provinces/{id}` |
| R2 | Correctly nested scoped collections where a collection only makes sense under a parent | Scoping | `/api/provinces/{id}/districts`, `/api/districts/{id}/substations`, `/api/substations/{id}/installations` |
| R3 | Installation **composite** resource — installation + its most relevant related data | Composite resources | `/api/installations/{id}/details` |
| R4 | **Last-known-reading** per installation (operational, real-time) as a *derived* resource | Derived / processing resources | `/api/installations/{id}/last-reading` |
| R5 | **Scoped readings sub-collection** — generation history for one installation | Analytical history | `/api/installations/{id}/readings` |

### Write path — the device

| # | Capability | Design area | Shape |
|---|-----------|-------------|-------|
| W1 | Ingest a new reading — correct method, **201 Created**, and a `Location` header pointing at the new resource | Create semantics + headers | `POST /api/installations/{id}/readings` |
| W2 | Correct create / retrieve / update / delete semantics across writable resources, with correct method choice and idempotency | Full CRUD, idempotency | `POST`/`GET`/`PUT`/`DELETE` on installations, substations, districts, provinces |

> **Idempotency note:** `PUT` is idempotent — repeat it, same end state. `POST` is not — repeating a
> reading push creates a duplicate. We handle this with the unique index on
> `(SolarInstallationId, Timestamp)`, which returns **409 Conflict** on a genuine duplicate retransmit.

### Advanced behaviour on the history (analytical surface)

| # | Capability | Shape |
|---|-----------|-------|
| A1 | **Pagination** returning total count + links to next and previous chunks | `?page=2&pageSize=50` → body has `totalCount`, `next`, `prev` |
| A2 | **Filtering** by jurisdiction (province / district / substation) and by time window | `?provinceId=`, `?districtId=`, `?substationId=`, `?from=`, `?to=` |
| A3 | **Sorting** by timestamp, ascending and descending | `?sort=timestamp&order=desc` |
| A4 | **Conditional GET** — `304 Not Modified` with empty body when client already holds current version | `If-None-Match` / `ETag`, `If-Modified-Since` |

### Error handling and security

| # | Capability | Shape |
|---|-----------|-------|
| E1 | **One consistent error-body schema** across the whole API, carrying `code`, `message`, and supporting detail | See schema in §6 |
| S1 | **Authentication on the write path** — a device authenticates as its installation | JWT with installation identity claim |
| S2 | **Jurisdiction-scoped authorization on the read path** — a district user cannot read another district | Role + scope claims → query filter |

### Upper-band stretch: district generation summary

| # | Capability | Shape |
|---|-----------|-------|
| X1 | **District generation summary** — aggregate generation for a district (current total power + today's total energy across all installations) | `GET /api/districts/{id}/summary` |

This is a processing-style resource computed across many assets — exactly what an operational
dashboard consumes. It is an optional enhancement, but it is where the read path demonstrates its
full value.

---

## 6. Error Schema (decide once, use everywhere)

Every client error (4xx) — and ideally 5xx — returns this exact shape.

```json
{
  "code": "READING_FOR_INSTALLATION_MISMATCH",
  "message": "A device may only push readings for its own installation.",
  "detail": "Token is bound to installation 7f3a... but the request targeted 91b2...",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "timestamp": "2026-09-28T10:14:22.481Z"
}
```

**Rules:**
- `code` — machine-readable, `SCREAMING_SNAKE_CASE`, stable. Clients switch on this, not on `message`.
- `message` — human-readable, safe to show a user, never leaks internals.
- `detail` — optional supporting context for developers.
- `traceId` — correlates to server logs (W3C traceparent format).
- `timestamp` — UTC.

**Implementation path:** a single `ApiExceptionFilter` / `ProblemDetails`-based middleware plus a custom
exception hierarchy (`NotFoundException`, `ValidationException`, `ForbiddenScopeException`,
`DuplicateReadingException`) so no controller ever hand-rolls an error body.

**Status code contract:**

| Code | Used for |
|------|----------|
| 200 | Successful read, successful `PUT` |
| 201 | Successful `POST` create — **must** include `Location` header |
| 204 | Successful `DELETE` |
| 304 | Conditional GET, client cache is current — **empty body** |
| 400 | Malformed / unparseable request |
| 401 | Missing or invalid credentials |
| 403 | Authenticated but out of jurisdiction, or device targeting another installation |
| 404 | Resource does not exist |
| 409 | Duplicate reading retransmit, or state conflict |
| 422 | Request parses but fails validation |
| 500 | Unhandled server error |

---

## 7. Security Model

### 7.1 Authentication

Two actor types, distinguished by token claims:

| Claim | Device token | SLSEA user token |
|-------|-------------|------------------|
| `sub` | Installation id | User id |
| `role` | `device` | `national` / `provincial` / `district` |
| `installation_id` | ✔ the bound installation | — |
| `scope_province_id` | — | ✔ if provincial |
| `scope_district_id` | — | ✔ if district |

**Device identity:** a device authenticates **as its installation**. The installation id is baked into
the token. On `POST .../readings`, the handler must assert `token.installation_id == route installationId`
and reject otherwise with **403** — this is the single most important authorization check in the project.

**Transport:** JWT bearer tokens, HS256 with a signing key held in configuration/user-secrets (never
committed). Device tokens are long-lived because devices cannot perform interactive login; this is a
known trade-off, and a production deployment would use per-device certificates or mTLS instead.

### 7.2 Jurisdiction-scoped authorization

Reads are filtered at the **query** level, not by post-filtering in memory:

| Role | Sees |
|------|------|
| `national` | Everything |
| `provincial` | Only districts/substations/installations/readings inside their province |
| `district` | Only within their district |

**Implementation:** an `IQueryable` extension / EF Core **global query filter** keyed off the caller's
scope claims, so a district user physically cannot construct a query that returns another district's
rows. Out-of-scope access returns `403` on *specific-resource* requests (e.g. asking for another
district's id), while list access returns a *silently narrowed collection* — clients should not be able
to infer the existence of rows they are not entitled to see.

---

## 8. Target Endpoint Map

```
# Hierarchy — collections and atomic resources
GET    /api/provinces                            list (paged)
GET    /api/provinces/{id}                       atomic
GET    /api/provinces/{id}/districts             scoped collection
GET    /api/districts                            list
GET    /api/districts/{id}                       atomic
GET    /api/districts/{id}/substations           scoped collection
GET    /api/districts/{id}/summary               ← stretch: aggregate generation
GET    /api/gridsubstations                      list
GET    /api/gridsubstations/{id}                 atomic
GET    /api/gridsubstations/{id}/installations   scoped collection
GET    /api/installations                        list
GET    /api/installations/{id}                   atomic
GET    /api/installations/{id}/details           ← composite resource
GET    /api/installations/{id}/last-reading      ← derived / operational
GET    /api/installations/{id}/readings          ← analytical history (paged, filtered, sorted)
POST   /api/installations/{id}/readings          ← device ingestion (201 + Location)

# Writable resources — full CRUD
POST   /api/provinces            PUT /api/provinces/{id}            DELETE /api/provinces/{id}
POST   /api/districts            PUT /api/districts/{id}            DELETE /api/districts/{id}
POST   /api/gridsubstations      PUT /api/gridsubstations/{id}      DELETE /api/gridsubstations/{id}
POST   /api/installations        PUT /api/installations/{id}        DELETE /api/installations/{id}

# Auth
POST   /api/auth/token           device and user token issuance
```

**Pagination envelope** (all list endpoints):

```json
{
  "data": [ /* ... */ ],
  "page": 2,
  "pageSize": 50,
  "totalCount": 672,
  "totalPages": 14,
  "links": {
    "self":  "/api/installations/7f3a/readings?page=2&pageSize=50&sort=timestamp&order=desc",
    "next":  "/api/installations/7f3a/readings?page=3&pageSize=50&sort=timestamp&order=desc",
    "prev":  "/api/installations/7f3a/readings?page=1&pageSize=50&sort=timestamp&order=desc"
  }
}
```

---

## 9. Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Presentation      Controllers, Filters, Middleware,        │
│                    Swagger config, Auth config              │
├─────────────────────────────────────────────────────────────┤
│  Application       Services (business logic), DTOs,         │
│                    Mapping profiles, Validators,            │
│                    Pagination/sorting helpers               │
├─────────────────────────────────────────────────────────────┤
│  Domain            Entities, Enums, Domain exceptions,      │
│                    Scope rules                              │
├─────────────────────────────────────────────────────────────┤
│  Infrastructure    DbContext, Configurations, Migrations,   │
│                    Seeder, Repositories, JWT provider       │
└─────────────────────────────────────────────────────────────┘
```

**Rules we hold ourselves to:**
- Controllers are **thin** — no EF queries in a controller, only service calls.
- Entities are **never** returned directly — always mapped to DTOs. This prevents over-posting and
  keeps the wire contract stable.
- Separate **create** and **update** DTOs, so a client cannot set `Id`.
- Swagger is configured with **XML doc comments**, token auth, and example values — the documentation
  surface is a first-class product, not an afterthought.

---

## 10. Non-Functional Requirements

| Concern | Decision |
|---------|----------|
| **Deployment** | Public, HTTPS, seeded. Localhost-only is **rejected**. |
| **OpenAPI** | Live Swagger UI served *from the deployment*. |
| **Git history** | Incremental commits, one logical change per commit. Repository shared with the module leader as a collaborator. |
| **Config/secrets** | Connection strings and JWT keys via user-secrets / environment only. Never committed. |
| **Performance** | Index on `GenerationReading(SolarInstallationId, Timestamp)`; `AsNoTracking()` on all reads; projection to DTO in the query. |
| **CORS** | Open for the module, tightened in config for a real deployment. |

---

## 11. Build Order (step-by-step)

Each step is a **branch + commit(s)**. This ordering keeps each step independently testable and builds
an incremental git history, so the repository shows real development progress rather than one bulk
upload.

| Step | Deliverable | Branch suggestion |
|------|-------------|-------------------|
| **0** | Install tooling (`dotnet` SDK; `git` on PATH), confirm README/.gitignore present on the working branch | `chore/bootstrap` |
| **1** | Solution + Web API project scaffold; put the project on a source branch (`dev`) | `chore/scaffold` |
| **2** | Domain entities + enums (`Province` → `GenerationReading`, `User`, role enum) | `feat/domain-model` |
| **3** | `DbContext`, entity configurations, indexes, relationships, migration #1 | `feat/db-context` |
| **4** | Seed generator (deterministic, bulk-inserted, diurnal shape) + migration #2 or seeding hook | `feat/seed-data` |
| **5** | Read path R1–R2: hierarchy collections, atomic resources, scoped nesting + pagination envelope | `feat/read-hierarchy` |
| **6** | R3–R5: composite resource, last-known-reading, readings sub-collection | `feat/derived-resources` |
| **7** | Analytical surface A1–A3: pagination, jurisdiction + time-window filtering, sorting | `feat/readings-query` |
| **8** | Error handling E1: custom exception hierarchy + global filter + consistent body | `feat/error-handling` |
| **9** | Auth S1–S2: JWT issuance, device identity, jurisdiction scoping, query filters | `feat/security` |
| **10** | Write path W1–W2: ingestion with 201 + `Location`, full CRUD, idempotency/409 | `feat/write-path` |
| **11** | Conditional GET A4: ETag / `If-None-Match` / `304` | `feat/conditional-get` |
| **12** | Stretch X1: district generation summary | `feat/district-summary` |
| **13** | Swagger polish: XML docs, examples, auth button, response codes | `docs/swagger` |
| **14** | Deploy to public HTTPS host; smoke-test every endpoint remotely | `chore/deploy` |
| **15** | Finalise README (real endpoint table, deployment URL), tag release, share repo with module leader | `docs/final` |

> **Deployment note:** the seed is ~134k rows. Free tiers with small storage quotas may not accept it —
> size the database plan before seeding, or reduce the window to keep the deployment viable while still
> satisfying "pagination must matter" (which a single installation's 672 rows already does).

---

## 12. Definition of Done

- [ ] Deployed, public, HTTPS, seeded, returns real data on every documented endpoint
- [ ] Live Swagger UI served from the deployment and complete
- [ ] All R1–R5, W1–W2, A1–A4, E1, S1–S2 implemented
- [ ] Stretch X1 implemented (target)
- [ ] Every 4xx returns the one error schema
- [ ] 201 on create carries a correct `Location` header
- [ ] 304 on conditional GET carries an empty body
- [ ] A district user provably cannot read another district
- [ ] A device provably cannot write to another installation
- [ ] Incremental commit history, repo shared with module leader
- [ ] Every design decision in §3.1, §5, and §7 recorded in the Design Decisions Register (§13)

---

## 13. Design Decisions Register

Every significant decision, with the reasoning that produced it. Keep this current as the
implementation evolves — it is the engineering record of the project.

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | No `Device` entity; `MeterId` is a column on `SolarInstallation` | A device has no independent identity or lifecycle in this domain. A separate table would add a join and a surrogate key for no new meaning. |
| D2 | `GenerationReading` is its own append-only table | The analytical read path ("how has generation behaved over time") is unanswerable from last-value columns. History is the product. |
| D3 | Surrogate `Id` key, unique index on `(SolarInstallationId, Timestamp)` | Devices retransmit on network failure. Keying on timestamp collapses legitimate retries; a unique index detects them and yields `409`. |
| D4 | `POST` for reading ingestion, not `PUT` | `POST` to a collection creates a new subordinate resource, which is exactly what a reading is. `PUT` would imply an already-known address. |
| D5 | `400` vs `422` kept distinct | `400` = the request could not be parsed. `422` = it parsed but failed domain validation. The client learns whether to fix syntax or semantics. |
| D6 | Jurisdiction filtering applied in the `IQueryable`, not after materialisation | A post-filter still fetches other jurisdictions' rows and leaks their existence through counts and timing. Query-level scoping is correct and efficient. |
| D7 | Reads use `AsNoTracking()` with projection straight to DTOs | The read path is high-volume and analytical; tracking entities we never modify wastes memory and time. |
| D8 | Entities never returned directly — always mapped to DTOs | Prevents over-posting on writes and decouples the wire contract from the schema. |
| D9 | Separate create/update DTOs | A client can never set `Id` or server-managed fields such as `IngestedAt`. |
| D10 | One error schema, produced by a single exception filter | Clients get one predictable shape for every failure; controllers never hand-roll error bodies. |
| D11 | Pagination envelope with `totalCount` + `next`/`prev` links | Consumers need to know the size of the history and how to walk it without constructing URLs themselves. |
| D12 | Seed generated deterministically (fixed random seed) with a diurnal curve | Every endpoint returns realistic data, and repeated runs produce identical datasets for reproducible testing. |
| D13 | Seed inserted in batches, not row-by-row | ~134k rows via per-row `SaveChanges()` takes minutes; batched `AddRange`/`SqlBulkCopy` takes seconds. |
| D14 | Composite and derived resources (`/details`, `/last-reading`) are separate endpoints | They answer different questions than the raw entity and have different caching and cost profiles. Keeping them separate keeps the atomic resource simple. |
| D15 | `Location` header mandatory on `201` | A created resource must report where it now lives, so the client never has to guess the new URL. |

---

## 14. Project Conventions

| Aspect | Convention |
|--------|-----------|
| **Commit messages** | `<type>: <description>` — `feat`, `fix`, `docs`, `chore`, `refactor`, `test` |
| **Branch names** | `feat/<area>`, `fix/<area>`, `chore/<area>` — see §11 |
| **Resource naming** | Plural nouns, kebab-case (`/api/gridsubstations`), nested only where a child cannot exist without its parent |
| **DTO naming** | `<Entity>Response`, `<Entity>CreateRequest`, `<Entity>UpdateRequest` |
| **JSON casing** | `camelCase` on the wire, PascalCase in C# |
| **Timestamps** | Always UTC, ISO-8601 (`DateTimeOffset`), never local time |
| **Paging defaults** | `page=1`, `pageSize=50`, maximum `pageSize=200` |
| **Sorting defaults** | `sort=timestamp`, `order=desc` (newest first) |
| **Secrets** | User-secrets locally, environment variables in deployment — never in `appsettings*.json` committed to git |