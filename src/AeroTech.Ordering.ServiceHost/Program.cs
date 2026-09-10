using AeroTech.Ordering.Providers.Deterministic;
using AeroTech.Framework.Infrastructure;
using AeroTech.Framework.Presentation.Extensions;
using AeroTech.Ordering.Application;
using AeroTech.Ordering.Consumers;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Persistence;
using AeroTech.Ordering.Providers;
using AeroTech.Ordering.Query;
using AeroTech.Ordering.ReferenceData;
using AeroTech.Ordering.RestApi;
using AeroTech.Ordering.ServiceHost.CallerContext;
using AeroTech.Ordering.ServiceHost.OperatorContext;
using AeroTech.Ordering.Synchronizer;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext().WriteTo.Console());

builder.Services.AddScoped<ICallerContext, ClaimsCallerContext>();
builder.Services.AddScoped<IHomeOperatorProvider, ReferenceDataHomeOperatorProvider>();

builder.Services
    .AddFrameworkInfrastructure(builder.Configuration)
    .AddPersistence(builder.Configuration)
    .AddProviders(builder.Configuration)
    .AddDeterministicProvidersWhenEnabled(builder.Configuration)
    .AddQuery(builder.Configuration)
    .AddSynchronizer()
    .AddConsumers(builder.Configuration)
    .AddApplication(builder.Configuration)
    .AddReferenceData(builder.Configuration)
    .AddPresentation(builder.Configuration, typeof(RestApiAssembly).Assembly, typeof(ReferenceDataAssembly).Assembly);

var app = builder.Build();

app.UsePresentation();

app.Run();

public partial class Program
{
}
