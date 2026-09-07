using System.Net;
using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Framework.Presentation.Responses;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AeroTech.Framework.Presentation.Middlewares
{
    public sealed class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                var (statusCode, response) = Map(exception);

                if (statusCode >= (int)HttpStatusCode.InternalServerError)
                    _logger.LogError(exception, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
                else
                    _logger.LogWarning("{ExceptionType} processing {Method} {Path}: {Message}", exception.GetType().Name, context.Request.Method, context.Request.Path, exception.Message);

                context.Response.StatusCode = statusCode;
                await context.Response.WriteAsJsonAsync(response);
            }
        }

        private (int StatusCode, ApiResult Response) Map(Exception exception)
        {
            switch (exception)
            {
                case ValidationException validation:
                    return ((int)HttpStatusCode.BadRequest, Error(new ApiErrorItem
                    {
                        Title = "Validation failed",
                        Detail = "One or more validation errors occurred.",
                        Metadata = validation.Errors
                            .GroupBy(failure => failure.PropertyName)
                            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray())
                    }));

                case BusinessException business:
                    return (business.HttpStatus, Error(new ApiErrorItem { Code = business.Code, Title = business.Message }));

                case ForbiddenException forbidden:
                    return ((int)HttpStatusCode.Forbidden, Error(new ApiErrorItem { Title = forbidden.Message }));

                default:
                    return ((int)HttpStatusCode.InternalServerError, Error(new ApiErrorItem
                    {
                        Title = _environment.IsDevelopment()
                            ? $"{exception.GetType().Name}: {exception.Message}"
                            : "An unexpected error occurred."
                    }));
            }
        }

        private static ApiResult Error(ApiErrorItem error) => new() { Errors = new[] { error } };
    }
}
