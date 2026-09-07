using AeroTech.Ordering.ReferenceData.Syncing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AeroTech.Ordering.ReferenceData.Api
{
    [ApiController]
    [Tags("Syncers")]
    [Route($"Syncer/v{{version:apiVersion}}")]
    public sealed class ReferenceSyncController : ControllerBase
    {
        private readonly CurrencySyncer _currencySyncer;
        private readonly AirlineSyncer _airlineSyncer;
        private readonly CitySyncer _citySyncer;
        private readonly AirportSyncer _airportSyncer;
        private readonly CustomerSyncer _customerSyncer;

        public ReferenceSyncController(
            CurrencySyncer currencySyncer,
            AirlineSyncer airlineSyncer,
            CitySyncer citySyncer,
            AirportSyncer airportSyncer,
            CustomerSyncer customerSyncer)
        {
            _currencySyncer = currencySyncer;
            _airlineSyncer = airlineSyncer;
            _citySyncer = citySyncer;
            _airportSyncer = airportSyncer;
            _customerSyncer = customerSyncer;
        }

        [HttpPost("Currencies")]
        public async Task<IActionResult> SyncCurrencies(CancellationToken cancellationToken)
        {
            await _currencySyncer.SyncAsync(cancellationToken);
            return Ok();
        }

        [HttpPost("Airlines")]
        public async Task<IActionResult> SyncAirlines(CancellationToken cancellationToken)
        {
            await _airlineSyncer.SyncAsync(cancellationToken);
            return Ok();
        }

        [HttpPost("Cities")]
        public async Task<IActionResult> SyncCities(CancellationToken cancellationToken)
        {
            await _citySyncer.SyncAsync(cancellationToken);
            return Ok();
        }

        [HttpPost("Airports")]
        public async Task<IActionResult> SyncAirports(CancellationToken cancellationToken)
        {
            await _airportSyncer.SyncAsync(cancellationToken);
            return Ok();
        }

        [HttpPost("Customers")]
        public async Task<IActionResult> SyncCustomers(CancellationToken cancellationToken)
        {
            await _customerSyncer.SyncAsync(cancellationToken);
            return Ok();
        }
    }
}
