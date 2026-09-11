# 07 - Eligibility, Lifecycles and Safe Business Operations

**Binding v1 specification.** This is the replacement for the old global OrderStateMachine, not an optional later enhancement. Policy decisions below are AeroTech design decisions; they are not claimed to be IATA-prescribed code. Business-state labels in this document are semantic; their concrete C# representation follows the existing repository/framework.

## 1. The guard contract

Each business command has a named, deterministic, I/O-free policy. The application obtains the evidence and the domain evaluates it.

```text
EligibilityContext
  OwnerAirlineId, OrderId, ExpectedCommercialVersion
  ActorAuthoritySnapshot, OperationType, TargetServiceIds[], EvaluatedAt
  CommercialStateSnapshot, CurrentMembershipSnapshot
  AcceptedTermsRefs[], QuoteRef?, QuoteExpiry?, QuoteTargetVersion?
  ReservationEvidence[], FundingEvidence[], DocumentControlEvidence[]
  DeliveryEvidence[], FlightEvidence[], ExistingOperationClaim?

EligibilityDecision
  Allowed | Denied | PendingEvidence
  ReasonCodes[]                       # stable, user-explainable codes
  EffectiveScopeServiceIds[]          # includes dependent/coupled scope
  RequiredActions[]                   # e.g. ObtainQuote, ReleaseControl, Reconcile
  EvidenceVersionVector
  PolicyVersion, ValidUntil?
```

Missing positive evidence is PendingEvidence, not Allowed and not a fabricated failure. A stale read-model row is not authoritative command evidence. UI may request a preview; execution always reevaluates. For irreversible actions, also require a valid external guarantee/control response when the external state can change independently. No configurable local timeout can substitute for ownership/guarantee evidence.

Common guards on every user/system business mutation: authenticated owner, correct seller/office or audited airline override, current target membership, valid expected version, nonconflicting operation claim, declared source/policy, idempotency receipt and allowed lifecycle transition. Inbound verified provider facts are accepted even when a customer is no longer authorized or an Order is closed; the facts are recorded, and incompatible business consequences become reconciliation work.

## 2. Eligibility by business operation

The rows below define **business gates**, not required handler/class/enum names. Organize implementation according to the existing AeroTech framework. Each public/internal operation must enforce the equivalent semantics.

| Operation family | Positive evidence required | Must not be inferred / unsafe completion |
|---|---|---|
| Create from accepted offer/direct-sale decision | trusted unexpired accepted commercial decision; known FinancialCustomer; valid service/beneficiary scope; reconciled accepted commercial total | missing price source, expired acceptance, unknown service ownership, guessed currency/tax/price fields |
| Add product/ancillary | Order serviceable; accepted product/price decision or explicit complimentary authority; valid beneficiary/coverage/dependencies | attach ancillary to replaced/cancelled flight without disposition; invent price/refundability because catalogue metadata is incomplete |
| Reserve capacity | active/pending accepted capacity obligation; current inventory product; no equivalent unresolved reserve operation | treating timeout as rejection/success; new reserve key while prior outcome unknown |
| Request payment/coverage | explicit accepted payable requirement and scope; authorized tender/credit path; no conflicting unresolved economic intent | infer amount from Order summary when a servicing quote is required; issue a second charge for Unknown outcome |
| Issue ETKT/EMD | active eligible services or approved monetary EMD purpose; required reservation evidence; confirmed funding/credit authority; valid issuer/coupon/control evidence; no equivalent unresolved issue | issue from Pending/Unknown funding, stale capacity, unavailable control, or duplicate unknown document operation |
| Pre-ticket cancel/withdraw | explicit scope/dependencies; approved cancellation treatment where money can change; reservation/coverage release plan | call it ticket refund when no document exists; assume captured money was returned because reservation was cancelled |
| Void issued document | issuer/provider says exact document/coupons are voidable; current coupon/control evidence; accepted commercial reversal treatment | hard-code a void window from one vendor; void used/exchanged/refunded coupon; equate document void with PSP refund completion |
| Voluntary cancel/refund | trusted unexpired refund/cancellation decision for exact scope; current used/unused/document evidence; original value/payment references; no duplicate unresolved refund | use allocation as refund amount; infer refundable tax/fare; complete commercial+cash result from only one confirmation |
| Voluntary itinerary/service change | accepted reshop/change decision; explicit old/new/preserved service scope; fare-context/dependency treatment; protected replacement capacity and required funding; document plan | mutate other traveler/segments not authorized by decision; replace delivered services; collapse unknown provider result into success |
| Revalidation | accepted change plus issuer/provider capability confirming same accountable document can be rebound; required value/rule/control evidence | copy vendor-specific revalidation criteria into Ordering; disguise an exchange-required change as revalidation |
| Exchange/reissue | accepted change/refund decision; eligible old coupons; replacement document plan; old/new document/service lineage; accepted fare/tax/penalty/residual/add-collect treatment | delete old document; recalculate price locally; issue another successor while prior exchange outcome unknown |
| Involuntary reaccommodation/change | current authoritative disruption/recovery instruction; exact affected scope; accepted recovery option; approved involuntary/waiver/value treatment; reservation/document readiness | infer zero fare/zero penalty only from `involuntary`; process obsolete recovery instruction |
| No-show/failure-to-use disposition | authoritative operational no-show evidence **plus** explicit commercial/Pricing decision if any monetary/cancellation consequence is requested | automatically cancel remaining sectors, forfeit value or create no-show penalty from DCS status alone |
| Ancillary servicing after flight/product change | explicit dependent-service review; supplier/Pricing disposition; EMD/control outcome where applicable | leave paid ancillary bound to replaced flight accidentally; invent pro-rata refund from service allocation |
| Cancel/reverse refund or correct prior document operation | provider/issuer explicitly permits corrective operation; exact prior operation/document/payment refs; reconciliation of actual external state | delete previous servicing record or simply flip local status back |
| Split Order | proper traveler subset; infant/dependency validity; shared-service partition support; provider divide outcome; balanced accepted value/payment transfer plan | duplicate shared room/car/pooled allowance; expose same payment/capacity to both orders |
| Traveler/name correction | authorized correction vs passenger substitution is clear; issuer/DCS/Pricing plan where issued travel is affected | universal assumption that name change is free/simple/reissue; transfer ticket to another person under correction operation |
| Time-limit extend/expire | current limit/evidence; provider extension where external hold is involved; no unresolved irreversible protecting step | extend external inventory locally without acknowledgment; expire while payment/issue outcome is Unknown |
| Close Order | no active future commercial obligations without disposition; no unresolved external/economic/document operation; required financial disposition recorded | close because UI summary looks terminal while a refund/issue/reconciliation is pending |
| Group create/materialize/release | valid customer/contract; per-flight capacity; name/deposit deadlines; stable row/slot identities; confirmed deposit/credit evidence as required | fabricate travelers for unnamed seats; sum capacity across different flight blocks; cancel spawned issued Orders with parent group |
| Manual commercial adjustment | explicit authorized business reason, affected monetary scope, source evidence and reconciliation | use manual adjustment as a backdoor for missing tax/FX/penalty/refund calculation |

Inbound authoritative provider facts remain recordable even when a user could no longer initiate the operation. If the new fact conflicts with current commercial state, retain the fact and route the consequence to reconciliation/manual review rather than discarding external reality.


## 3. Current commercial state transitions

### OrderService

| From | To | Required business intent |
|---|---|---|
| Pending | Active | Valid acceptance/activation with required local data |
| Pending | Cancelled or Expired | Cancellation/expiry decision |
| Active | Cancelled | Finalized cancellation decision with withdrawal evidence |
| Active | Replaced | Finalized replacement; successor links mandatory |
| Active | Expired | Explicit unsatisfied service validity/hold-expiry commercial consequence |
| Cancelled / Replaced / Expired | same state | Idempotent same fact only |
| Cancelled / Replaced / Expired | Active | Forbidden; use a new service or a separately authorized correction process |

Active means accepted commercial service, not capacity confirmed, paid, issued or consumed. A consumed active service remains part of the historical accepted sale. Adding a delivery observation never implicitly calls Activate/Cancel/Replace.

### OrderItem summary

Historical predecessor/partitioned items are explicitly Replaced/Partitioned and never recomputed from services now owned elsewhere. For a current item: all Pending -> Pending; all Active -> Active; all Cancelled -> Cancelled; all Expired -> Expired; all Replaced -> Replaced; any mixed active/pending/terminal collection -> PartiallyChanged. Empty current deliverable items cannot be accepted. Financial-only fees are price lines; documented EMD-S monetary purpose does not fabricate an empty service item.

### Order summary (persisted)

Evaluate in this order: ClosedAt set -> Closed; any current Active service -> Active; nonempty current scope entirely Cancelled -> Cancelled; no Active services and at least one Expired/Replaced/other terminal service -> Inactive; otherwise Draft. Mixed pending/terminal facets remain available separately. This summary is queryable but never the sole eligibility guard.

## 4. Reservation and document lifecycles

Reservation member statuses are Pending, Confirmed, Rejected, Released, Expired or Unknown. The root summarizes counts and HasUnknown; when members differ use Mixed, not a false Confirmed. An externally confirmed new reservation generation is not allowed to overwrite the state of an older released generation. Pending/Unknown resolve only from correlated results or authoritative reconciliation.

Coupon financial transitions:

| From | To | Guard |
|---|---|---|
| Open | Used | Authoritative actual consumption, correct service/flight scope |
| Open | Void | Whole-document void eligibility and provider/local confirmation |
| Open | Exchanged | Successful exchange/linkage to confirmed successor document |
| Open | Refunded | Coupon refund/withdrawal confirmation; cash completion remains separate |
| Open | Suspended | Authorized suspension; record reason/control evidence |
| Suspended | Open | Authorized reinstatement, current control and version |
| Used / Void / Exchanged / Refunded | same | Same event idempotent no-op |
| Any terminal | other terminal or Open | No ordinary transition; explicit authoritative correction with evidence/reason and reconciliation |

For an externally owned document, a local command records intent until confirmation. For a locally owned document, eligible mutation and document version commit locally. Do not confuse refund of a fare difference with refunding an entire coupon.

`ControlStatus` is orthogonal: Local -> External only after ownership transfer acknowledgment; External -> ReleasePending on request; ReleasePending -> Local after release acknowledgment; timeout -> Unknown. A Redis lock is not airport coupon control. Checked-in/boarded passengers require certified DCS reversal/control rules; preserve observed milestones even after control is released.

Document header statuses are recomputed from coupon states: Issued (all usable), PartiallyUsed/Mixed, Used/Closed, Void, Exchanged or Refunded as appropriate. Header summary does not override individual coupon eligibility.

## 5. Durable operation protocol

`ServicingOperation` is durable APPLICATION workflow state, not another business Order aggregate. Reuse/enrich existing FulfillmentTask/ProviderInteraction machinery; introduce a parent operation record only because several irreversible steps need one stable identity.

```text
ServicingOperation
  OperationId, OwnerAirlineId, OrderId, CommandReceiptId
  Kind, Status, RequestHash, TargetServiceIds[], ExpectedCommercialVersion
  QuoteRef?, EvidenceVersionVector, ClaimGeneration
  PlannedChangeSnapshot, ConfirmedStepResults[], CreatedAt, UpdatedAt

OperationStatus
  Prepared | Executing | AwaitingExternal | ReadyToFinalize
  Committed | Completed | Rejected | Compensating | NeedsReconciliation
```

Prepared -> Executing after durable intent. Executing -> AwaitingExternal for Pending/Unknown, -> ReadyToFinalize when all required evidence is confirmed, -> Rejected only when all steps are definitively not executed or safely compensated. ReadyToFinalize -> Committed after local commercial transaction, -> Completed once required downstream acknowledgments/dispositions are recorded. Any contradiction or incomplete compensation -> NeedsReconciliation; Committed can remain visible with a pending downstream financial step. Do not rollback already executed external reality by deleting the operation.

Each step has a stable StepId and external idempotency key. Retry updates an Attempt counter but keeps OperationId/StepId/normalized request hash. A legitimate new purchase/refund request gets a NEW business OperationId, not a retry key masquerading as a new attempt.

### Concurrency simplification

One durable exclusive servicing claim per Order is the v1 default, regardless of operation name (Issue vs Cancel vs Split share the SAME claim namespace). A filtered unique index prevents two unresolved holders. ClaimGeneration is a fencing token, checked on finalization. Technical Redis leases may reduce contention but do not decide business ownership.

Lease expiry is a liveness signal; it NEVER releases an unresolved payment/issue/cancel/divide claim automatically. Recovery takes over the same operation and reconciles its steps. Administrative override requires a reason and confirmed treatment of existing side effects. DCS/flight/payment observations still persist during a claim; the finalizer reevaluates newer evidence before accepting a commercial action.

For two-order split/transfer, acquire claims in ascending OrderId and finalize the local pair in one SQL transaction. External payment transfer is reserved/confirmed under one TransferGroupId. Unknown outcomes preserve the recovery operation; they never expose two usable copies of the same funds.

## 6. Operation finalization checkpoints

The following checkpoints separate external reality from local commercial finalization. Exact adapter step order may differ only when the certified provider contract requires it and the same safety properties remain true.

| Operation | Safe preparation | Local/commercial finalization checkpoint | Uncertainty behavior |
|---|---|---|---|
| Reserve | persist request/scope/stable key | apply confirmed per-service/coupling result | retain Pending/Unknown and reconcile same operation |
| Issue | eligibility + funding authority + stock/document intent | confirmed external issue result or one local authoritative document commit | preserve reserved number/step; reconcile before any new issue |
| Pre-ticket cancel | cancellation treatment + release plan | required capacity/coverage release evidence + one commercial cancellation commit | do not repeat cancellation; unresolved releases remain visible |
| Void | issuer eligibility + document/control evidence + stable void intent | confirmed document void then accepted commercial treatment | document/payment outcomes reconciled independently; no blind second void |
| Voluntary cancel/refund | accepted refund decision + document/control plan + stable payment-refund intent | commercial cancellation/credit committed once after required withdrawal evidence; cash/value result tracked separately | Pending/Unknown refund does not cause a second commercial credit |
| Change | accepted reshop/change plan + replacement capacity/funding protection | required supplier/document result confirms the plan, then commit service replacement + price change once | retain before/after plan; reconcile actual external state; explicit compensation only if contract supports it |
| Revalidation | accepted change + provider revalidation capability/control | provider confirms rebinding of existing document/coupons | remain unresolved or use explicitly approved alternate plan; do not fabricate reissue |
| Exchange/reissue | accepted change + successor document plan + funding/value disposition | confirmed successor document and old/new lineage, then one commercial finalization | retain original/successor refs and reconcile same operation if ambiguous |
| Involuntary recovery | current disruption instruction + accepted recovery/value treatment | confirmed replacement/withdrawal/document steps then commit recovery change | report Pending/Rejected/Reconciliation back to disruption owner; no duplicate replacement |
| No-show disposition | authoritative no-show + explicit commercial/Pricing decision | only the approved cancellation/penalty/forfeit/reuse action | NoShow observation alone never reaches commercial finalization |
| Ancillary reassociation/refund | dependent-service disposition + supplier/EMD plan | confirmed keep/reassociate/replace/cancel result + accepted price treatment | active service remains exception/manual review until owner result is known |
| Split | stable child identity + accepted partition + provider/payment protections | commit source/child current ownership and balanced commercial lines once | retry same child/transfer; never create second child/value |

Finalization always produces enough immutable references for later `ServicingRecord` redisplay. Payment completion may happen after commercial finalization when the approved flow permits asynchronous refund; that pending value movement remains explicitly visible and prevents false “all settled” summaries.


## 7. Default policy decisions that remove ambiguity

- Partial reservation: retain confirmed holds, reconcile unknowns, cancel/release only by a visible operation; no automatic all-or-nothing assertion across services.
- Partial issue: keep issued documents, retry/reconcile unresolved members under original keys; no duplicate documents or pretending entire order failed.
- Missing compulsory tax/price/document evidence: block the affected irreversible operation; unrelated safe services remain visible.
- No-show: record authoritative observation; return journey changes only via configured fare/no-show decision and an explicit OrderChange. No automatic cancellation just because travel time passed.
- Unknown funding: not spendable. Confirmed credit guarantee is distinct from captured cash. Partial payment is supported; issuance uses scoped coverage, not `CapturedAmount == OrderTotal` globally.
- Voluntary versus involuntary cancellation is preserved end to end. An unknown external reason maps to explicit Other/Unmapped with raw code, never PassengerRequest by default.
- Same source financial fact and same version: no-op. A contradicting authoritative newer fact: persist and reconcile; do not throw away evidence because an old local status looked terminal.
- Authority, provider capability and regulatory parameters are versioned CONFIGURATION with validation; absent mandatory configuration fails startup/action eligibility. No generic policy-engine DSL is required.

## 8. Mandatory tests and reason codes

Policies have table-driven tests for allowed, denied, missing evidence, stale version and duplicate command cases. Minimum named reason codes:

`OrderClosed`, `WrongOwner`, `ServicingAuthorityDenied`, `StaleCommercialVersion`, `OperationInProgress`, `EvidenceUnavailable`, `EvidenceStale`, `QuoteExpired`, `QuoteScopeMismatch`, `ReservationUnknown`, `CoverageInsufficient`, `PaymentOutcomeUnknown`, `ControlNotReleased`, `DocumentAlreadyIssued`, `VoidWindowClosed`, `ServiceAlreadyUsed`, `SharedServiceCannotBePartitioned`, `InfantAdultSplitInvalid`, `PriceBreakdownMismatch`, `ReversalExceedsOriginal`, `IdempotencyPayloadConflict`, `ProviderCapabilityUnsupported`.

Adding a new service type normally requires a detail validator, a fulfillment profile and scenario cases, not another payment model or global order state machine. Adding a new provider changes its adapter/evidence mapping, not the commercial sign or ownership rules.
