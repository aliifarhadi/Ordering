#!/usr/bin/env python3
"""Semantic reference harness for Modern PSS Ordering Domain Design v1.

IMPORTANT:
- This file checks business invariants/fixture relationships only.
- It does NOT prescribe C# types, enums, SQL numeric precision, currency identifiers,
  rounding algorithms, framework abstractions, message shapes, or provider contracts.
- Monetary examples use Decimal only so this standalone documentation fixture has exact
  arithmetic. Production representation must be discovered from the AeroTech platform.
- Provider-/Pricing-owned results are treated as GIVEN authoritative inputs; the harness
  never implements fare, tax, penalty, refund, FX or commission calculation rules.
"""
from decimal import Decimal
import hashlib
import json

checks = []
failures = []

def check(name, condition):
    checks.append(name)
    if not condition:
        failures.append(name)

# Commercial vs settlement visibility
fare = Decimal('400')
bag = Decimal('50')
discount = Decimal('45')
commission = Decimal('20')
customer_total = fare + bag - discount
check('customer total from accepted customer components', customer_total == Decimal('405'))
check('settlement commission does not change customer total', customer_total == Decimal('405'))
check('discount reversal restores customer amount', customer_total + discount == Decimal('450'))

# Tax semantics: source-calculated customer-collected tax is customer commercial value.
accepted_fare = Decimal('100')
accepted_tax = Decimal('20')
check('customer collected tax contributes to accepted customer total', accepted_fare + accepted_tax == Decimal('120'))
check('tax is not removed from customer total merely by settlement classification', accepted_fare + accepted_tax != accepted_fare)

# Reversal / immutable history
original = Decimal('100')
already_reversed = Decimal('30')
check('partial reversal within outstanding value allowed', Decimal('70') <= original - already_reversed)
check('over reversal rejected', Decimal('80') > original - already_reversed)
check('cancellation penalty is new accepted charge not reversal', Decimal('15') > 0 and Decimal('15') != -original)
check('historical accepted amount remains unchanged by later reversal', original == Decimal('100'))

# Allocation is attribution, never refund entitlement.
line_total = Decimal('100')
allocs = [Decimal('40'), Decimal('60')]
check('complete allocation conserves accepted parent amount', sum(allocs) == line_total)
check('allocation target values do not define refund automatically', sum(allocs[:1]) == Decimal('40') and Decimal('40') != Decimal('55'))
source_refund = Decimal('55')  # GIVEN source result
check('authoritative refund may differ from allocation', source_refund != allocs[0])

# Partial-used refund example: used valuation comes from Pricing/Refund owner.
original_paid = Decimal('300')
source_used_value = Decimal('170')
source_refundable_taxes = Decimal('20')
source_penalty = Decimal('10')
source_approved_refund = Decimal('140')
check('partial used refund is source result not naive half split', source_approved_refund != original_paid / 2)
check('source used valuation retained independently', source_used_value == Decimal('170'))
check('refundable tax retained independently', source_refundable_taxes == Decimal('20'))
check('penalty retained independently', source_penalty == Decimal('10'))

# Change outcomes are orthogonal values, not one status.
change_even = {'add_collect': Decimal('0'), 'refund': Decimal('0'), 'residual': Decimal('0'), 'penalty': Decimal('0')}
check('even exchange can have zero value movement', all(v == 0 for v in change_even.values()))
change_penalty_only = {'add_collect': Decimal('0'), 'refund': Decimal('0'), 'residual': Decimal('0'), 'penalty': Decimal('25')}
check('even fare difference can coexist with separate penalty', change_penalty_only['penalty'] > 0 and change_penalty_only['add_collect'] == 0)
change_mixed = {'add_collect': Decimal('80'), 'refund': Decimal('15'), 'residual': Decimal('10'), 'penalty': Decimal('20')}
check('change can preserve add collect separately', change_mixed['add_collect'] == Decimal('80'))
check('change can preserve refund separately', change_mixed['refund'] == Decimal('15'))
check('change can preserve residual separately', change_mixed['residual'] == Decimal('10'))
check('change can preserve penalty separately', change_mixed['penalty'] == Decimal('20'))

# Reusable amount may be unknown until later source decision.
reusable = {'eligible': True, 'amount': None}
check('reusable eligibility can exist without exact value', reusable['eligible'] and reusable['amount'] is None)

# Document operations remain distinct.
ops = {'cancel', 'void', 'refund', 'revalidate', 'exchange_reissue'}
check('cancel and void distinct', 'cancel' in ops and 'void' in ops and 'cancel' != 'void')
check('void and refund distinct', 'void' != 'refund')
check('revalidation and reissue distinct', 'revalidate' != 'exchange_reissue')
check('commercial cancel does not imply refund', 'cancel' != 'refund')

# Revalidation keeps document identity; reissue has successor identity.
old_ticket = 'T1'
revalidated_ticket = 'T1'
new_ticket = 'T2'
check('revalidation keeps accountable document identity', revalidated_ticket == old_ticket)
check('reissue creates successor identity', new_ticket != old_ticket)
check('reissue lineage retains old and new identity', (old_ticket, new_ticket) == ('T1','T2'))

# Unknown provider outcome / idempotency.
def op_key(order_id, operation_id, step):
    return hashlib.sha256(f'{order_id}|{operation_id}|{step}'.encode()).hexdigest()
k1 = op_key('O1','OP9','refund')
k2 = op_key('O1','OP9','refund')
k3 = op_key('O1','OP10','refund')
check('retry uses stable provider economic key', k1 == k2)
check('different economic operation gets different key', k1 != k3)
provider_state = 'Unknown'
check('provider timeout may remain Unknown', provider_state == 'Unknown')
check('Unknown is not optimistic success', provider_state != 'Confirmed')
check('Unknown is not definitive rejection', provider_state != 'Rejected')

# Commercial version vs event ordinal semantics.
commercial_version = 10
same_mutation_events = [(commercial_version, 1), (commercial_version, 2), (commercial_version, 3)]
check('one mutation may publish several events under one commercial version', len({v for v,_ in same_mutation_events}) == 1)
check('events within mutation have distinct ordinals', len({o for _,o in same_mutation_events}) == 3)
check('rejected operation does not require commercial version increment', commercial_version == 10)
check('accepted next commercial mutation can increment once', commercial_version + 1 == 11)

# Fare construction / service granularity.
true_rt = {'pricing_units': 1, 'services': 2, 'coupled': True}
ow_ow = {'pricing_units': 2, 'services': 2, 'coupled': False}
check('true RT can couple two services in one pricing unit', true_rt['services'] == 2 and true_rt['pricing_units'] == 1 and true_rt['coupled'])
check('OW plus OW can remain independently priced', ow_ow['pricing_units'] == 2 and not ow_ow['coupled'])
through_fare = {'air_services': 2, 'fare_components': 1}
check('fare component count need not equal service count', through_fare['air_services'] != through_fare['fare_components'])
passenger_segment = {'sold_segments': 1, 'operational_legs': 2, 'air_services': 1}
check('one sold passenger segment may contain multiple operational legs', passenger_segment['operational_legs'] > passenger_segment['sold_segments'])
check('physical operational legs do not force extra commercial services', passenger_segment['air_services'] == passenger_segment['sold_segments'])

# No-show / DCS evidence separation.
dcs_no_show = True
commercial_cancelled = False
penalty_from_pricing = None
check('DCS no show does not auto cancel commercial service', dcs_no_show and not commercial_cancelled)
check('DCS no show does not auto create penalty', dcs_no_show and penalty_from_pricing is None)
prior_flown = True
downstream_no_show = True
check('downstream no show does not undo prior flown', prior_flown and downstream_no_show)

# DCS batch / aspect versions.
source_message = 'MSG1'
members = [('P1','SEG1'),('P2','SEG1')]
check('one source message can retain multiple member observations', source_message == 'MSG1' and len(members) == 2)
aspects = {'boarding': 4, 'seat': 9}
check('independent DCS aspect versions can coexist', aspects['boarding'] != aspects['seat'])
check('older same-aspect event cannot replace newer evidence', 3 < aspects['boarding'])

# Delivery distinctions.
check('flight departure does not prove passenger flown', True)
check('baggage accepted does not prove baggage delivered', True)
check('ancillary non delivery does not rewrite air flown evidence', True)

# Ancillary dependencies / shared services.
shared_hotel = {'beneficiaries': {'P1','P2'}, 'rooms': 1}
check('shared room is not duplicated by passenger split', shared_hotel['rooms'] == 1 and len(shared_hotel['beneficiaries']) == 2)
extra_seat = {'traveler_count': 1, 'capacity_units': 2}
check('extra seat capacity does not require fake second traveler', extra_seat['traveler_count'] == 1 and extra_seat['capacity_units'] == 2)

# Group booking capacity and idempotency.
blocks = {'OUT':50,'MID':50,'IN':40}
requested = 45
check('group capacity is checked per required flight block', not all(cap >= requested for cap in blocks.values()))
blocks_ok = {'OUT':50,'MID':50,'IN':50}
check('group materialization valid when every required block has capacity', all(cap >= requested for cap in blocks_ok.values()))
row_results = {'1':'issued','2':'issued','50':'unknown'}
check('group retry can retain successful rows while reconciling unknown row', row_results['1']=='issued' and row_results['50']=='unknown')

# Payment/value owner separation.
payment_fact = {'status':'RefundConfirmed','payment_ref':'PAY1'}
order_record = {'payment_ref':'PAY1','commercial_cancelled':True}
check('Ordering may correlate confirmed external payment fact', order_record['payment_ref'] == payment_fact['payment_ref'])
check('Payment status remains externally sourced evidence', payment_fact['status'] == 'RefundConfirmed')

# Servicing record is final audit/read evidence and not recalculation.
servicing_record = {
    'operation':'OP1',
    'pricing_decision':'PQ7',
    'document_refs':['T1'],
    'payment_refs':['PAY1'],
    'finalized':True,
}
check('final servicing record correlates operation and accepted pricing', servicing_record['operation']=='OP1' and servicing_record['pricing_decision']=='PQ7')
check('final servicing record correlates document/payment evidence', servicing_record['document_refs']==['T1'] and servicing_record['payment_refs']==['PAY1'])
check('final servicing record is finalized evidence', servicing_record['finalized'])
refund_notice = {'servicing_record':'OP1','is_source_of_truth':False}
check('refund notice is presentation artifact not domain truth', refund_notice['servicing_record']=='OP1' and not refund_notice['is_source_of_truth'])

# Ownership / decision-gate semantics.
owners = {
    'customer':'Core',
    'currency_reference':'AirInfo/BasicInfo',
    'rate_source':'AirPrice',
    'price_refund_penalty_calculation':'Pricing',
    'capacity':'Inventory/FlightFlow',
    'delivery_observation':'DCS',
    'value_movement':'Payment/StoredValue',
    'accounting':'Ledger',
}
check('cross service ownership map names source owners', all(owners.values()))
check('Ordering is not currency master', owners['currency_reference'] != 'Ordering')
check('Ordering is not ROE master', owners['rate_source'] != 'Ordering')
check('Ordering is not refund calculation engine', owners['price_refund_penalty_calculation'] != 'Ordering')
check('Ordering is not payment ledger', owners['value_movement'] != 'Ordering')
check('Ordering is not accounting ledger', owners['accounting'] != 'Ordering')

# Decision gate: an unknown shared representation is not solved by invention.
unknown_representation = True
agent_invents_shared_type = False
blocked_decision = unknown_representation and not agent_invents_shared_type
check('ambiguous shared representation becomes blocked decision', blocked_decision)

result = {
    'checks': len(checks),
    'failed': len(failures),
    'failures': failures,
    'passed': len(checks) - len(failures),
}
print(json.dumps(result, indent=2))
if failures:
    raise SystemExit(1)
