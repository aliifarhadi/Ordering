using AeroTech.Framework.Presentation.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AeroTech.Framework.Presentation.Filters
{
    public sealed class ApiResultWrapperFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executed = await next();

            if (executed.Result is not ObjectResult objectResult)
                return;

            if (objectResult.Value is ApiResult)
                return;

            if (objectResult.Value is null)
            {
                executed.Result = new ObjectResult(new ApiResult()) { StatusCode = objectResult.StatusCode };
                return;
            }

            var wrapperType = typeof(ApiResult<>).MakeGenericType(objectResult.Value.GetType());
            var wrapper = Activator.CreateInstance(wrapperType, objectResult.Value);

            executed.Result = new ObjectResult(wrapper) { StatusCode = objectResult.StatusCode };
        }
    }
}
