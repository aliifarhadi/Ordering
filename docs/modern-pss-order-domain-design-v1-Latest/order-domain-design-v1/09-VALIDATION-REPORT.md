# 09 - Validation Report

**Version: v1, revised in place. Date: 2026-09-07.**

## 1. What was actually executed

The delivered `reference_harness.py` was executed from this package and completed **71 semantic checks: 71 passed, 0 failed**.

The harness was deliberately rewritten in this pass so it **does not define or imply** AeroTech production classes, enums, SQL numeric precision, currency identifiers or rounding algorithms. It uses standalone exact-decimal fixtures only where arithmetic is useful for demonstrating a business invariant. Production representation remains a P0 repository/contract discovery decision under document `13`.

The reference checks cover:

- separation of customer-effective and settlement-only commercial values;
- tax/customer-value safety and immutable monetary history;
- reversal bounds and allocation conservation without treating allocation as refund entitlement;
- partially used refund as an authoritative source result rather than pro-rata arithmetic;
- even exchange/add-collect/refund/residual/penalty as independent servicing outcomes;
- cancel/void/refund/revalidate/reissue distinction and document lineage;
- stable provider operation identity and Unknown reconciliation semantics;
- CommercialVersion/event-ordinal separation;
- true RT versus independent OW+OW and passenger-segment/operational-leg boundaries;
- no-show/DCS evidence separation, ancillary/shared-service behavior, group per-block capacity;
- ServicingRecord/RefundNotice source-of-truth distinction;
- source ownership and the `BLOCKED_DECISION` rule for ambiguous shared representations.

It does **not** execute EF Core, SQL Server, RabbitMQ, Redis, AeroTech production code, a real Pricing/Tax engine, DCS, Payment provider or document issuer and cannot prove provider/load/concurrency behavior. Those remain implementation release gates.

Reference harness SHA-256: `36b0ee502f598469c4368328b35d3344de41daaf7e57ee1f49ed13ae2d274551`.

Run:

```bash
python reference_harness.py
```

Expected summary:

```json
{
  "checks": 71,
  "failed": 0,
  "failures": [],
  "passed": 71
}
```

## 2. Servicing benchmark pass executed

The post-sale domain was rechecked across **IATA Offers & Orders/AIDM, ATPCO Categories 16/31/33 and Optional Services, Amadeus refund/reissue/revalidation workflows, Travelport exchange/refund/void/structured-fare-rule/EMD flows, and Sabre Offers & Orders / schedule-change / ticket-control evidence**. Detailed conclusions and source register are in `12-SERVICING-BENCHMARK-AND-FINANCIAL-FLOWS.md`.

This evidence pass confirmed the design must keep distinct:

- pre-ticket cancellation, document void, voluntary cancellation/refund;
- informative reshop versus accepted commercial change;
- revalidation versus exchange/reissue;
- fare/tax differences, penalties, add-collect, refunds and residual/reusable value;
- customer money movement versus document outcome;
- voluntary versus involuntary authority/reason;
- no-show observation versus later commercial/financial decision;
- ancillary service change versus EMD/document/value outcome;
- final servicing audit/redisplay record versus customer-facing notice/receipt.

The benchmark does **not** authorize copying vendor internal models or implementing a local ATPCO/fare/refund engine. Carrier/provider-specific eligibility remains source-driven.

## 3. Scenario-catalogue consistency

The package contains **308 unique consecutive scenario IDs, S-001 through S-308**.

- S-001..S-130: original sale/pricing/servicing/reliability baseline.
- S-131..S-220: flight topology, carrier/DCS/ancillary/pricing/group/fan-out completion pass.
- S-221..S-308: final servicing and financial benchmark pass: cancellation/void/refund, partial-used refunds, revalidation/reissue, add-collect/residual/reusable, penalties/waivers/no-show, ancillary/EMD servicing, planned-schedule customer-decision windows, involuntary recovery, name correction, servicing records/notices and no-assumption/platform-decision gates.

`10-FLIGHT-ANCILLARY-COVERAGE-MATRIX.md` maps the major risk families to these scenarios. No new business aggregate was required by the final servicing pass.

## 4. Mechanical document checks required before packaging

Packaging validation checks:

- scenario IDs exactly consecutive S-001..S-308 with no duplicates;
- Markdown code fences balanced;
- every relative Markdown link in `MANIFEST.md` resolves;
- `reference_harness.py` exists, exits 0 and reports 71/71;
- no implementation-specific decimal/rounding policy remains as a mandatory domain rule;
- `13` and the first-agent prompt explicitly forbid agent-created shared/cross-service semantics;
- P0-P6 have a bounded reading map;
- P3 references the complete servicing benchmark and S-221..S-308;
- P5 contains the high-fan-out runtime gate;
- ZIP integrity succeeds.

These are document consistency checks, not .NET implementation evidence.

## 5. Design review versus runtime evidence

`DESIGN-REVIEWED` means the model and required source/guard behavior can represent the case. It does not mean carrier/provider policy has been guessed or the code passed. `CONFIGURED POLICY` means a trusted airline/provider/Pricing policy/result must supply the decision. `BLOCKED_DECISION` means implementation must stop the affected path rather than manufacture a platform/business rule.

The following remain runtime release gates:

| Runtime gate | Required evidence |
|---|---|
| P0 Framework/platform boundary | repository evidence map; existing primitive reuse; .NET/SQL transaction/concurrency/failure tests; all material ambiguities resolved or blocked |
| Pricing/tax/refund/penalty | real accepted sale/servicing result contracts and source semantics; no local invented rule engine |
| Payment/value | real Payment/StoredValue contract, stable idempotency and Unknown/reconciliation tests |
| ETKT/EMD | certified issuer/provider capability, document control, stock, partial/unknown issue/void/refund/reissue reconciliation |
| P4 DCS | certified mappings, batches, ordering/corrections, control release/ACK and load behavior |
| P5 FlightOps | FlightCancelled 1,000 Orders <=30s baseline, bounded per-Order transactions, no cross-Order/provider I/O lock, race tests |
| Third-party ancillary | real supplier adapter/capability for sell/change/cancel/refund/delivery before operational claim |
| Group | SQL/batch/idempotency tests including partial materialization/issue/payment outcomes |
| Notices/receipts | confirmed platform owner/integration; Ordering must not invent a renderer/notification platform if ownership is unresolved |

## 6. Outcome of the final review

The domain remains intentionally compact:

- `Order` is commercial truth;
- `OrderItem` is the accepted priced grouping;
- `OrderService` is stable service/servicing identity;
- fare construction is optional accepted pricing context;
- immutable pricing history records accepted commercial results without becoming a pricing engine;
- ETKT/EMD/DocumentStock/FulfillmentReservation and GroupBooking retain their required consistency boundaries;
- delivery/FlightOps/disruption facts remain evidence/projections, not sale aggregates;
- `ServicingRecord` is final redisplay/audit evidence and is **not** a new aggregate by default;
- Refund/Exchange Notice and receipts are derived artifacts owned by the platform capability discovered in P0.

The two major corrections from this final review are (1) the earlier passenger-segment versus operational-leg distinction and (2) a full servicing/financial separation plus explicit platform ownership/decision gates. Neither requires a generic workflow engine or proliferation of aggregates.
