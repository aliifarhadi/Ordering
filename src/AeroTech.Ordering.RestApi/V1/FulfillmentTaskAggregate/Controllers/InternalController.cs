using AeroTech.Ordering.Application.FulfillmentTaskAggregate.Commands.RetryFulfillmentTask;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate.Commands.RunFulfillmentTask;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AeroTech.Ordering.RestApi.V1.FulfillmentTaskAggregate
{
    [ApiController]
    [ApiVersion("1.0")]
    [Tags("Internal")]
    [Route($"Internal/v{{version:apiVersion}}/FulfillmentTasks")]
    public sealed class InternalController : ControllerBase
    {
        private readonly IMediator _mediator;

        public InternalController(IMediator mediator) => _mediator = mediator;

        [HttpPost("{id:long}/Run")]
        public async Task<IActionResult> Run(long id, CancellationToken cancellationToken)
        {
            var outcome = await _mediator.Send(new RunFulfillmentTaskCommand(id), cancellationToken);
            return Ok(outcome);
        }

        [HttpPost("{id:long}/Retry")]
        public async Task<IActionResult> Retry(long id, CancellationToken cancellationToken)
        {
            var outcome = await _mediator.Send(new RetryFulfillmentTaskCommand(id), cancellationToken);
            return Ok(outcome);
        }
    }
}
