using Azure.Provisioning;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.PostgreSql;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
                    .RunAsContainer(postgres =>
                    {
                        postgres.WithDataVolume();
                        postgres.WithPgAdmin(pgAdmin =>
                        {
                            pgAdmin.WithHostPort(5050);
                        });
                    })
                    .ConfigureInfrastructure(infra =>
                    {
                        var pg = infra.GetProvisionableResources().OfType<PostgreSqlFlexibleServer>().Single();

                        infra.Add(new ProvisioningOutput("hostname", typeof(string))
                        {
                            Value = pg.FullyQualifiedDomainName
                        });
                    });

var templateAppDb = postgres.AddDatabase("TemplateAppDB", "TemplateApp");

var keycloakPassword = builder.AddParameter("KeycloakPassword", secret: true, value: "admin");
var keycloak = builder.AddKeycloak("keycloak", adminPassword: keycloakPassword)
                      .WithLifetime(ContainerLifetime.Persistent);

if (builder.ExecutionContext.IsRunMode)
{
    keycloak.WithDataVolume()
            .WithRealmImport("./realms");
}

if (builder.ExecutionContext.IsPublishMode)
{
    var postgresUser = builder.AddParameter("PostgresUser", value: "postgres");
    var postgresPassword = builder.AddParameter("PostgresPassword", secret: true);
    postgres.WithPasswordAuthentication(userName: postgresUser, password: postgresPassword);

    var keycloakDb = postgres.AddDatabase("keycloakDB", "keycloak");

    var keycloakDbUrl = ReferenceExpression.Create(
        $"jdbc:postgresql://{postgres.GetOutput("hostname")}/{keycloakDb.Resource.DatabaseName}"
    );

    keycloak.WithEnvironment("KC_HTTP_ENABLED", "true")
            .WithEnvironment("KC_PROXY_HEADERS", "xforwarded")
            .WithEnvironment("KC_HOSTNAME_STRICT", "false")
            .WithEnvironment("KC_DB", "postgres")
            .WithEnvironment("KC_DB_URL", keycloakDbUrl)
            .WithEnvironment("KC_DB_USERNAME", postgresUser)
            .WithEnvironment("KC_DB_PASSWORD", postgresPassword)
            .WithEndpoint("http", e => e.IsExternal = true);
}

var keycloakAuthority = ReferenceExpression.Create(
    $"{keycloak.GetEndpoint("http").Property(EndpointProperty.Url)}/realms/templateapp"
);

var healthPort = 8081;

builder.AddProject<TemplateApp_Api>("templateapp-api")
        .WithReference(templateAppDb)
        .WaitFor(templateAppDb)
        .WithEnvironment("Auth__Authority", keycloakAuthority)
        .WithEnvironment("SWAGGERUI_CLIENTID", builder.Configuration["SwaggerUI:ClientId"])
        .WaitFor(keycloak)
        .WithExternalHttpEndpoints()
        .PublishAsAzureContainerApp((infra, containerApp) =>
        {
            var container = containerApp.Template.Containers.Single().Value;

            container?.Probes.Add(new ContainerAppProbe
            {
                ProbeType = ContainerAppProbeType.Liveness,
                HttpGet = new ContainerAppHttpRequestInfo
                {
                    Path = "/health/alive",
                    Port = healthPort,
                    Scheme = ContainerAppHttpScheme.Http
                },
                PeriodSeconds = 10
            });

            container?.Probes.Add(new ContainerAppProbe
            {
                ProbeType = ContainerAppProbeType.Readiness,
                HttpGet = new ContainerAppHttpRequestInfo
                {
                    Path = "/health/ready",
                    Port = healthPort,
                    Scheme = ContainerAppHttpScheme.Http
                },
                PeriodSeconds = 10
            });
        })
        .WithEnvironment("HTTP_PORTS", $"8080;{healthPort.ToString()}")
        .WithHttpHealthCheck("/health/ready");

builder.AddAzureContainerAppEnvironment("cae");

builder.Build().Run();