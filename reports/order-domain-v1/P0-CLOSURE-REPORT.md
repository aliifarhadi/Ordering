# P0 — Closure Report

**Repository:** `E:\Projects\DotAir\Ordering` · **Branch:** `k8s-stg` · **Date:** 2026-09-08
**Scope:** Modern PSS Ordering Domain Design v1, phase **P0 only**. P1 was not started.

Durable audit record: [`audit/P0-DISCOVERY-AND-DECISIONS.md`](audit/P0-DISCOVERY-AND-DECISIONS.md)
Raw build/test evidence: [`audit/P0-TEST-RUN.txt`](audit/P0-TEST-RUN.txt)

> `/docs/` is git-ignored in this repository, so the durable audit record was moved here. The copy under
> `docs/order-domain-design-v1/implementation/` is a working convenience only and is not the record.

---

## 1. Status

**P0 is complete.**

| Build / test | Result |
|---|---|
| `dotnet build AeroTech.Ordering.sln` | **0 errors**, 2 warnings |
| `AeroTech.Ordering.Domain.Tests` | **25 passed**, 0 failed |
| `AeroTech.Ordering.Persistence.Tests` | **36 passed**, 0 failed (real SQL Server) |
| Total | **61 passed, 0 failed** |

---

## 2. P0 exit criteria

| Criterion | Result | Evidence |
|---|---|---|
| All unblocked P0 work builds and tests successfully | met | §1 above; `audit/P0-TEST-RUN.txt` |
| Current Identity/context consumption is compatible with the actual platform contract | met | audit §10; `IdentityService` re-pointed at the issued claims, `ICallerContext` replicated from Aegis |
| Reservation-only changes do not advance `CommercialVersion` | met | audit §11; `CommercialVersionTests`, `CommercialVersionReplayTests` |
| Command receipts cannot collide across authenticated caller scopes | met | audit §10.5–10.6; 13 domain + 8 persistence isolation tests |
| No duplicate Framework subsystem introduced | met | audit §8, §10.4; the only Framework changes are the additive `IInboxStore` pair agreed under SV-017 and a claim-name correction inside the existing `IdentityService` |
| No Mock provider treated as production evidence | met | `MockPaymentProvider` remains bound and is excluded from every eligibility and release gate; BD-3 |
| Remaining blockers are genuinely P1/later dependencies | met | §4 below |
| Complete evidence written to this report | met | this file plus the audit record |

---

## 3. What this session added on top of the earlier P0 work

### 3.1 Identity and authenticated-context audit — a live defect was found and fixed

Ordering's Framework identity accessor read `UserId`, `CustomerId`, `DeviceId` and `FullScope`. **None of
those claims exist in the current platform token** (`IdentityServer` →
`IdentityTokenClaims`). The practical consequence was not theoretical:

- `CurrentUserId` returned `null` on every current token, so **every aggregate was audit-stamped with a
  null actor and every integration event was published with `Actor = null`**;
- `RequiredCurrentUserId` would throw `NullReferenceException` in three live servicing handlers
  (add remark, cancel order, void document).

Fixed by reusing what the platform already proves rather than inventing anything:

- `ICallerContext` + `ClaimsCallerContext` replicated from **Aegis's canonical shape**, binding to the
  **shared** `AeroTech.Messages` enums (`BusinessContextType`, `PrincipalType`, `AuthorizationSurface`);
- `IdentityService` re-pointed at the issued claims, keeping the `IIdentityService` signature so all six
  call sites and the audit trail keep working;
- `CheckAccess` and `RequiredDeviceId` deliberately left alone — rewriting `CheckAccess` would recreate
  Aegis authorization policy inside Ordering. Recorded as debt **I-2**.

### 3.2 `CallerScope` resolved and implemented

`{AuthorizationSurface}|{AuthorizationContextScopeKey}|{Subject:<sub> | Client:<client_id>}`, where the
middle segment reuses the platform's existing `AeroTech.Messages.Aegis.AuthorizationContextScopeKey`.
A context type whose identifying claim is missing is **rejected (403/2705) rather than collapsed** into a
shared scope — collapsing is exactly what would let one caller replay another caller's idempotency key.

### 3.3 Verification of the earlier P0 changes

All eight earlier changes were re-tested rather than accepted on compilation, including the paths a
compile cannot reach: single-physical-connection proof, ambient-transaction governance, rollback in both
directions, expired-lease non-release, unsaved-resolve non-release, stale-generation fencing on both
finalization paths, and 6-way concurrent claim contention. Three **test** defects were found and fixed;
no production defect was found in the earlier work. Detail in audit §11.

---

## 4. BLOCKED_DECISIONs

| ID | Subject | Status after this session |
|---|---|---|
| **BD-1a** | The trusted operator-context source for `OwnerAirlineId` | **Still blocked.** The owner confirmed it is trusted operator/deployment context and not a JWT claim. A search across `JetPay/src`, `StoredValue/src` and `Aegis/src`, plus `IdentityTokenClaims`, `ICallerContext` in Aegis and Core, and Ordering's `appsettings.json`, found **no trusted operator-context provider anywhere in the platform**. This is a missing platform capability, not a missing lookup. Also gates the hardcoded `IntegrationEventOptions.TenantId = 1` (F-7). |
| **BD-2** | `RoundingFactor` semantics and type (`double` in AirInfo vs `int` in Ordering) | **Downgraded to deferred contract debt** by owner decision. No rounding abstraction created; the field is untouched and unread. Only a future boundary that needs the exact value would block, and only that boundary. |
| **BD-3** | The Ordering↔JetPay production payment contract (JP-002 Requested; JP-010 Ledger credit half blocking) | **Re-scoped to P1** by owner decision. Not a P0 blocker. It *is* a blocker for enabling real payment-backed issuance and refund execution. |

BD-1's `CallerScope` half is **resolved and implemented**; only the `OwnerAirlineId` half survives as BD-1a.

---

## 5. Cross-service ledger

`SV-017` (inbox dedup race) — **Ordering side implemented, tested and closed**; the item is now Done
across all four affected services. The ledger file and index at `E:\Projects\DotAir\handoffs\` were
updated with the Ordering log entry and a ticked checklist. **No sibling repository was modified.**

Ordering-owned items left untouched because they are P1 or later: `SV-011`, `SV-012`, `JP-002`, `JP-007`.

---

## 6. Critical unresolved issue

**BD-1a.** `OwnerAirlineId` is part of the `CommandReceipt` uniqueness key and stamps every published
integration event, and the platform currently has no trusted operator-context provider at all. Until the
owner names the source, the receipt write path cannot be wired and `TenantId` stays hardcoded to `1`. The
columns, the unique constraint and the isolation tests are in place, so closing this is a small change —
but it must be closed before P1 issues receipts.

---

## 7. Not started

P1 was not begun, as instructed. No sale, issue, payment or refund workflow was implemented; no new HTTP
endpoints were added; no compatibility layer for current consumers was created.
