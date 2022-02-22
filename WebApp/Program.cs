using System.Diagnostics;
using Honeycomb.OpenTelemetry;
using MqPublisher;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Define some important constants and the activity source
var serviceName = "OTelTest_WebApp";
var serviceVersion = "1.0.0";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MessagePublisher>();

// Configure OpenTelemetry settings
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(serviceName)
    .AddSource(nameof(MessagePublisher))
    .SetResourceBuilder(
        ResourceBuilder.CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
    .AddAspNetCoreInstrumentation()
    .AddJaegerExporter(o =>
    {
        o.AgentHost = "localhost";
        o.AgentPort = 6831;
    })
    .AddOtlpExporter(configure =>
    {
        configure.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        configure.Endpoint = new Uri("https://my-deployment-5de0aa.apm.westeurope.azure.elastic-cloud.com");
        configure.Headers = $"Authorization=ApiKey <REMOVED>";
    })
    .AddHoneycomb(new HoneycombOptions
    {
        ServiceName = serviceName,
        ApiKey = "<REMOVED>",
        Dataset = "my-dataset"
    })
    .AddConsoleExporter()
    .Build();

var app = builder.Build();

var words = new[] {"quick", "brown", "fox", "jumped", "over", "the", "lazy", "dog"};

var wordIndex = 0;

var MyActivitySource = new ActivitySource(serviceName);

app.MapGet("/word", async () =>
    {
        using var activity = MyActivitySource.StartActivity($"get_word");
        string result = null;
        result = words[wordIndex++ % words.Length];
        activity?.SetTag("word", result);
        using (var publisher = new MessagePublisher())
        {
            await publisher.PublishAsync(result);
        }
        return result;
    });

app.MapGet("/reset", () =>
    {

        wordIndex = 0;
        return "ok";

    });

app.Run();
