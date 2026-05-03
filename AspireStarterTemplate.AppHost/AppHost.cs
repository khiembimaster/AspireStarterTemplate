var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var agilePmDb = postgres.AddDatabase("agile-pm-db");
var collaborationDb = postgres.AddDatabase("collaboration-db");
var identityAccessDb = postgres.AddDatabase("identity-access-db");

var rabbitmq = builder.AddRabbitMQ("rabbitmq");

var keycloak = builder.AddKeycloak("keycloak")
    .WithBindMount("./keycloak/realms", "/opt/keycloak/data/import");

var redis = builder.AddRedis("redis");

var agileApp = builder.AddProject<Projects.AgileProjectManagement_Application>("agile-pm")
    .WithReference(agilePmDb)
    .WaitFor(agilePmDb)
    .WithHttpHealthCheck("/health");

var collaborationApp = builder.AddProject<Projects.Collaboration_Application>("collaboration")
    .WithReference(collaborationDb)
    .WaitFor(collaborationDb)
    .WithHttpHealthCheck("/health");

var identityApp = builder.AddProject<Projects.IdentityAccess_Application>("identity-access")
    .WithReference(identityAccessDb)
    .WaitFor(identityAccessDb)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(agileApp)
    .WithReference(collaborationApp)
    .WithReference(identityApp)
    .WithReference(redis)
    .WaitFor(agileApp)
    .WaitFor(collaborationApp)
    .WaitFor(identityApp)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
