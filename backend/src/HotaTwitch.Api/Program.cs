using HotaTwitch.Api.Endpoints;
using HotaTwitch.Application;
using HotaTwitch.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapHealthEndpoints();
app.MapStateEndpoints();
app.MapConfigEndpoints();

await app.RunAsync();
