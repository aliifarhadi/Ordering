# P2-C.1 — Fare Construction Scope Resolution

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
Baseline: P2-C, 369 tests. P0–P2-A.1 frozen, P2-B/B.1 frozen. **P2-D not started.**

Evidence: [`audit/p2/P2-C.1-TEST-RUN.txt`](audit/p2/P2-C.1-TEST-RUN.txt)

---

## 1. Result

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | 0 errors |
| `AeroTech.Ordering.Domain.Tests` | **202 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **178 passed**, 0 failed (real SQL Server) |
| Total | **380 passed, 0 failed** (baseline 369 → +11, zero regressions) |

## 2. The defect

P2-C shipped a global-singular assumption:

```csharp
CurrentFareConstruction()            // picked ONE non-superseded construction by CreatedAt
ActiveFareComponentFor(serviceId)    // searched only inside that one
```

An Order may legitimately carry several simultaneously active, independent constructions covering different
commercial scopes — construction A covering item A and service 101, construction B covering item B and
service 202. Both are current; there is no single "current fare construction" for the Order. Under the old
code, whichever construction had the later `CreatedAt` won, and every service outside it silently fell through
to the transitional legacy `FareBasis` — a wrong answer that looked like a valid absence.

## 3. The fix

`CurrentFareConstruction()` is replaced by `CurrentFareConstructions()`, returning **all** non-superseded
constructions. The supersession set was already branch-specific (a construction is superseded only if some
other construction names it), so `A1 → A2` alongside an untouched `B1` correctly yields `{A2, B1}` rather than
just the newest.

`ActiveFareComponentFor(orderServiceId)` no longer picks a construction first. It searches every current
construction for components that actually cover the requested service and resolves on the match count:

| Matches | Behaviour |
|---|---|
| 0 | returns `null` — valid for current-source / opaque-price Orders |
| 1 | that component is the authoritative fare context |
| >1 | **fails closed** with reason code **2807** (`AmbiguousActiveFareComponent`, HTTP 409) |

Nothing is chosen by newest construction, first row, highest id or latest `CreatedAt`, and no construction is
auto-superseded to resolve the conflict. Two active constructions claiming authoritative fare context for one
service is ambiguous commercial history and is surfaced, not guessed.

## 4. ETKT resolution

`ResolveIssueFareBasis(serviceId)` keeps the approved rule and inherits the fail-closed behaviour: unique
active component → its `FareBasis`; no active component → the explicitly transitional
`OrderAirTransportService.FareBasis`; ambiguity → 2807 propagates. The fallback is reserved for **absence** of
authoritative fare construction and is never reached through conflict — asserted by a test that proves the
legacy value exists yet is not used when ambiguity is present.

## 5. One small accepted-source addition

Testing branch-specific supersession required the accepted source to be able to express lineage, since
`SupersedesConstructionId` previously had no path in from the acceptance contract. `AcceptedFareConstruction`
gained an optional `SupersedesConstructionRef` — a source-local ref resolved against constructions accepted in
the same batch, with an unresolved ref failing as 2784. This is the minimum seam needed; it does **not**
implement the P3 reissue/exchange workflow, and no other fare-construction semantics changed.

## 6. Unchanged, as required

`PricingGroup`, `PricingUnit` and `FareComponent` fields and semantics; the current AirPrice behaviour of
emitting no fare construction; `PricingLine`/allocation monetary truth; ETKT value attribution; `FareBasis`
opacity; and the P2-C optional-construction rule. No schema change and therefore **no migration** — the fix is
purely resolution logic. No ancillary work was started.

## 7. Tests added (+11)

`tests/AeroTech.Ordering.Domain.Tests/P2/FareConstructionScopeTests.cs` (10) covers requirements 1–10 and 12:
two independent constructions both current; each service resolving from its own construction; creation order
and `CreatedAt` not affecting resolution (asserted over both orderings with an advanced clock); superseding one
branch leaving the unrelated branch current; a superseded construction ignored during resolution; zero matches
using the transitional fallback; exactly one match winning; two active components over one service failing
closed with 2807; ambiguity never silently falling back; and a through-fare component still covering several
services unambiguously through a single component identity.

`tests/AeroTech.Ordering.Persistence.Tests/P2/AirFareConstructionPersistenceTests.cs` gained requirement 11:
an order with two independent constructions is reserved and issued end-to-end, and every coupon's fare-basis
snapshot matches the resolver's answer for its own service — one coupon `YOUT`, the other `YIN`.

Requirement 13 holds: all P0–P2-C tests remain green.

## 8. Exit gate

| Gate | Status |
|---|---|
| No global-single-current-construction assumption | **Yes** — the singular API no longer exists |
| Multiple independent current constructions coexist correctly | **Yes** |
| Fare context resolves by actual service membership | **Yes** |
| Supersession is branch/scope aware | **Yes** |
| Ambiguous active fare context fails closed | **Yes** — 2807 |
| Legacy fallback only when authoritative construction is absent | **Yes** |
| All tests pass | **Yes** — 380 passed, 0 failed |

**P2-C is safe to freeze.** P2-D not started. No `BLOCKED_DECISION` required.
