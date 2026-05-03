var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("collaboration-db");

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", () => "Collaboration.Application");

app.Run();
