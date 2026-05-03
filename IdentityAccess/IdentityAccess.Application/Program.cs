var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("identity-access-db");

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", () => "IdentityAccess.Application");

app.Run();
