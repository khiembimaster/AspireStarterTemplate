var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("agile-pm-db");

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", () => "AgileProjectManagement.Application");

app.Run();
