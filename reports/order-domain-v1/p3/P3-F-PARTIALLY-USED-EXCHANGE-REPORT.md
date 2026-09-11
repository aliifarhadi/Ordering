# P3-F Capability Bundle — Partially-Used Even Reissue and Ordering-First Integration Contracts

Implementation report for the partially-used exchange capability.

Companion documents:

* [P3-integration-capability-catalog.md](P3-integration-capability-catalog.md) — the living integration contract catalog.
* [P3-F-FREEZE-GATE-CORRECTION-REPORT.md](P3-F-FREEZE-GATE-CORRECTION-REPORT.md) — the follow-up contract and recovery correction.

Test counts in section H are the counts at the time this bundle closed. The correction report carries the
final numbers.

---

## A. Capability result

Partially-used even reissue is implemented. A predecessor with at least one `Used` and at least one `Open`
coupon now reissues successfully.

* **Reissue scope** is every remaining `Open` coupon. Changed services become `Replaced`, unchanged ones
  `Continued`.
* **`Used` coupons** stay `Used`, get no successor coupon, no document-exchange entry, no inventory item, and
  never appear in the predecessor's exchange record. Their order service keeps its delivery history untouched.
* **Predecessor** becomes `Exchanged` after provider confirmation. The successor holds successors of the
  `Open` scope only. Lineage stays A→B, then A→B→C.
* Any coupon state other than `Open` or `Used` is refused as an application capability limit before any
  irreversible work, with code 2976 (`ExchangeCouponStateNotSupported`).
* A changed service covered only by a non-`Open` coupon is refused with code **2997**
  (`CouponIsNotExchangeable`), the most accurate existing exception. No new exception code was added. This
  retargets `ExchangeFlowTests.B2`, which previously expected 2915.

The F2 accountable-document resolver is unchanged:
`CurrentOrderServiceId == serviceId && FinancialStatus == Open`.

---

## B. AirPrice port evolution

`ExchangeQuoteRequest` gained the predecessor document number, a separate open scope, a separate historical
context, and the stored fare construction. The old `PredecessorCouponEvidence` type is deleted.

```text
AIRPRICE_CONTEXT_DECISION:
Two sibling collections of distinct record types.
  ExchangeScope:          ExchangeScopeCoupon[]
  HistoricalUsedCoupons:  HistoricalUsedCoupon[]
Rejected: one PredecessorCouponContext[] carrying a Role discriminator.
```

**WHY**, against the seven mandated criteria:

1. **Role explicit** — the role is the C# type and the field name, not a position or a flag value.
2. **Cannot leak into issuance** — `HistoricalUsedCoupon` has no `ServiceIsChanging` and no `CurrentSegment`,
   so it is structurally unusable where successor scope is built. A single type with a role field would have
   been assignable to either list.
3. **Both segment facts distinguishable** — the scope carries `CurrentSegment` plus `IssuedSegment`; the
   history carries `IssuedSegment` plus `CurrentBoundSegment`, which is `null` only when it equals the issued
   facts. Neither overwrites the other.
4. **Provider neutral** — plain records of primitives and `TicketedSegmentSnapshot`.
5. **No aggregates cross** — no `ElectronicTicket`, `TicketCoupon` or `Order` instance passes through.
6. **Local references are explicit** — `PredecessorTicketCouponId` and `CurrentOrderServiceId` are named as
   Ordering-owned correlation, never as external identity.
7. **No new persisted enum** — `TicketCouponFinancialStatus` is reused; nothing new was added to the contracts
   project.

**Fare-construction snapshot decision.** `Order.FareConstructionContexts()` projects the already-stored
`OrderAirFareConstruction` into four provider-neutral records covering groups, units and components. Nothing
is inferred and nothing is calculated. Absent or empty stays structurally valid.

Omitted deliberately: `BrandName`, `CreatedAt`, all foreign keys and all pricing line items, since none of
them is fare-construction topology.

---

## C. Execution scopes

| Scope | AirPrice | Inventory | Document Host | Successor ETKT |
| --- | --- | --- | --- | --- |
| `Used` historical context | Yes, as `HistoricalUsedCoupons` | No | No | No |
| `Open` continued | Yes, as `ExchangeScope` | No | Yes, as `Continued` | Yes |
| `Open` replaced | Yes, as `ExchangeScope` | Yes, one change item each | Yes, as `Replaced` | Yes |

---

## D. Integration contracts

| Capability | ICC status | Simulator | Contract tests | Real integration |
| --- | --- | --- | --- | --- |
| AirPrice quote/accept | Written | Extended, observes scope, history, pricing evidence and fare construction | Yes | `BLOCKED_INTEGRATION` |
| Inventory reservation change | Written | Unchanged, no gap exposed | Yes | `BLOCKED_INTEGRATION` |
| Document host exchange | Written | Unchanged, malformed-response knobs reused | Yes | `BLOCKED_INTEGRATION` |
| DCS usage evidence | Written as a dependency contract, not a port | Not applicable | Not applicable, state-driven flow tests instead | `BLOCKED_INTEGRATION` |

One finding worth flagging: **nothing inside Ordering writes `TicketCouponFinancialStatus.Used` today.**
`TicketCoupon` exposes `Void`, `Refund`, `RestoreFromRefund`, `RebindToService`, `MarkExchanged` and
`RecordProviderStatus`, and `RecordProviderStatus` has no caller anywhere in the source. There is no usage
consumer or projection. The capability reads and enforces the state correctly; its producer is an open
integration obligation. No synchronous DCS call was added.

---

## E. Recovery

| Case | Acceptances | Inventory applies | Document dispatches | Document recoveries |
| --- | --- | --- | --- | --- |
| Refused preflight, E F G J | 0 | 0 | 0 | 0 |
| Malformed accepted scope, K L | 1 | 0 | 0 | 0 |
| Stale version, Q | 0 | 0 | 0 | 0 |
| Inventory unknown, N | 1 | 1 | 0 | 0 |
| Crash after document dispatch, O | 1 | 1 | 1 | 1 |
| Host maps a `Used` coupon, M | 1 | 1 | 1 | 0 |

Case N holds the claim, so a second operation returns 2700. Case O recovers under the same operation key and
never dispatches twice. Case M reaches `NeedsReconciliation` with the plan marked document-confirmed, no
successor ticket, the predecessor still `PartiallyUsed`, and no order change.

---

## F. Edge matrix

All seventeen cases pass. A is the pre-existing fully-unused baseline in `MultiCouponExchangeFlowTests`;
B through Q are in `tests/AeroTech.Ordering.Persistence.Tests/P3/PartiallyUsedExchangeFlowTests.cs`.

| Case | Result | Case | Result |
| --- | --- | --- | --- |
| A fully unused baseline | Pass | J EMD on open scope, 2978 | Pass |
| B used plus one open | Pass | K accepted adds used coupon, 2979 | Pass |
| C replaced plus continued | Pass | L accepted omits open coupon, 2979 | Pass |
| D two changed | Pass | M host maps used coupon | Pass |
| E change used service, 2997 | Pass | N inventory unknown | Pass |
| F refunded coupon, 2976 | Pass | O crash after dispatch | Pass |
| G fully used, 2997 | Pass | P repeated reissue A to B to C | Pass |
| H revalidated then used | Pass | Q stale version, 2730 | Pass |
| I EMD on used only | Pass | | |

The obsolete `A_partly_used_document_is_refused_as_a_capability_limit_before_any_provider_call` was retired,
since its premise is now a supported success case.

---

## G. Schema/API impact

**None.** No migration was added, no table or column changed, and no REST contract or integration event
changed. The EMD block was narrowed from all predecessor coupons to the open reissue scope, which is a
behavior fix inside the existing check.

---

## H. Verification

During implementation only the affected projects were built, with the partial-use suite and the port
contracts run focused. One freeze gate at the end:

| Gate | Result |
| --- | --- |
| `dotnet build AeroTech.Ordering.sln` | Succeeded |
| Domain tests | 493 passed |
| Persistence tests | 669 passed |
| Partial-use suite, focused | 16 passed |
| Port contract kit, focused | 23 passed |

The contract kit lives in `tests/AeroTech.Ordering.Persistence.Tests/Contracts/`, one folder per port area,
each with a fixture, an abstract contract holding the semantic assertions, and a deterministic binding. A
future ACL adapter subclasses the same contract. It needs no database. No new test project was created.

---

## I. Blockers

```text
BLOCKED_DEVELOPMENT:
None.
```

```text
BLOCKED_INTEGRATION:
1. No ingestion path writes TicketCouponFinancialStatus.Used. A usage ingestion
   (integration event, consumer, and a domain transition on ElectronicTicket) is
   required before real partial-use servicing can occur.
2. Consumed-operational-segment evidence is not stored. Issue-time and current-bound
   segments are both reported where they differ; neither is the flown segment.
3. AirPrice: unverified whether it accepts the open-scope / historical-used split, the
   stored fare-construction snapshot, a caller-supplied replay-safe operation key, or
   returns a partially-used Even outcome at all.
4. Inventory: unverified whether FlightFlow exposes a plan-level replace with a stable
   caller key and a WasDispatched read-back.
5. Document host: unverified whether it accepts a coupon-subset reissue of a partially
   used document, correlates on document number plus predecessor coupon number without
   Ordering-local ids, and exposes a WasDispatched read-back.
6. TicketCouponControlStatus is persisted but has no writer, so coupon control is not yet
   part of the exchange decision.
```
