using HotaTwitch.Api;
using HotaTwitch.Api.Endpoints;
using HotaTwitch.Application;
using HotaTwitch.Domain.Broadcasting;
using HotaTwitch.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.Limits.MaxRequestBodySize = BroadcastPolicy.MaxStateDocumentBytes);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddExtensionCors(builder.Configuration);

var app = builder.Build();

app.UseCors();

app.MapHealthEndpoints();
app.MapStateEndpoints();
app.MapConfigEndpoints();

await app.RunAsync();
