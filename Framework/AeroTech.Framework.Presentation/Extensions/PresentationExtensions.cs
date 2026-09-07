using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Framework.Presentation.AspNetCore.Services;
using AeroTech.Framework.Presentation.Filters;
using AeroTech.Framework.Presentation.HealthChecks;
using AeroTech.Framework.Presentation.Json;
using AeroTech.Framework.Presentation.Middlewares;
using AeroTech.Framework.Presentation.Swagger;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace AeroTech.Framework.Presentation.Extensions
{
    public static class PresentationExtensions
    {
        public static IServiceCollection AddPresentation(
            this IServiceCollection services,
            IConfiguration configuration,
            params Assembly[] controllerAssemblies)
        {
            services.AddHttpContextAccessor();
            services.TryAddSingleton<IIdentityService, IdentityService>();

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    var jwtKey = configuration["JwtSecrets:Key"];
                    if (string.IsNullOrWhiteSpace(jwtKey))
                        return;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                        ValidateIssuer = !string.IsNullOrWhiteSpace(configuration["JwtSecrets:Issuer"]),
                        ValidIssuer = configuration["JwtSecrets:Issuer"],
                        ValidateAudience = !string.IsNullOrWhiteSpace(configuration["JwtSecrets:Audience"]),
                        ValidAudience = configuration["JwtSecrets:Audience"],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });

            var mvc = services
                .AddControllers(options => options.Filters.Add<ApiResultWrapperFilter>())
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.JsonSerializerOptions.Converters.Add(new LongToStringJsonConverter());
                    options.JsonSerializerOptions.Converters.Add(new NullableLongToStringJsonConverter());
                });

            foreach (var assembly in controllerAssemblies)
                mvc.AddApplicationPart(assembly);

            services
                .AddApiVersioning(options =>
                {
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.ReportApiVersions = true;
                })
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter a JWT bearer token."
                });

                options.DocumentFilter<BearerSecurityRequirementDocumentFilter>();
            });
            // "self" is a dependency-free check tagged "live": it succeeds as long as the
            // process can serve HTTP, which is exactly what a Kubernetes liveness probe wants.
            services
                .AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), new[] { "live" });

            return services;
        }

        public static WebApplication UsePresentation(this WebApplication app)
        {
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AeroTech Ordering API v1");
                    options.RoutePrefix = string.Empty;
                });
            }

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            // Kubernetes liveness probe: only the dependency-free "live" checks.
            // Failure here means the process is wedged and the pod should be restarted.
            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains("live"),
                ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
            });

            // Kubernetes readiness probe: dependency checks tagged "ready" (SQL Server,
            // Redis, message bus). Failure pulls the pod out of the Service load balancer
            // without restarting it, so it can recover once dependencies come back.
            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains("ready"),
                ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
            });

            // Aggregate endpoint (every registered check) — handy for humans/dashboards.
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
            });

            return app;
        }
    }
}
