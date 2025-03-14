using BuildingBlocks.Behaviors;
using BuildingBlocks.Exceptions.Handler;
using Catalog.API.Data;
using HealthChecks.UI.Client;
using Marten.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.Grafana.Loki;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

Serilog.Debugging.SelfLog.Enable(Console.Out);
builder.Host.UseSerilog((context, config) =>
{
	config
		.ReadFrom.Configuration(context.Configuration); // Read from appsettings.json;
});

// Configure OpenTelemetry Tracing
builder.Services.AddOpenTelemetry()
	.WithTracing(tracerProviderBuilder =>
	{
		tracerProviderBuilder
			.AddAspNetCoreInstrumentation()  // Capture incoming HTTP requests
			.AddHttpClientInstrumentation() // Capture outgoing HTTP requests
			.AddSource("Marten")
			.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Catalog.API"))
			.AddOtlpExporter(options =>
			{
				options.Endpoint = new Uri(builder.Configuration.GetValue<string>("OtelTrace:Endpoint")!);
			});
	})
	.WithMetrics(metrics =>
	{
		metrics.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Catalog.API"))
			.AddAspNetCoreInstrumentation() // Capture request metrics
			.AddRuntimeInstrumentation() // Capture .NET runtime metrics
			.AddMeter("Marten") // Custom application metrics
			.AddOtlpExporter(opt =>
			{
				opt.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
				opt.Endpoint = new Uri(builder.Configuration.GetValue<string>("OtelMetric:Endpoint")!);
			})
			.AddConsoleExporter((exporterOptions, metricReaderOptions) =>
			{
				metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
			}); 
	}); 



var assembly = typeof(Program).Assembly;
builder.Services.AddMediatR(config =>
{
	config.RegisterServicesFromAssembly(assembly);
	config.AddOpenBehavior(typeof(LoggingBehavior<,>));
	config.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(assembly);

builder.Services.AddMarten(opts =>
{
	opts.Connection(builder.Configuration.GetConnectionString("Database")!);

	// Turn on Otel tracing for connection activity, and
	// also tag events to each span for all the Marten "write"
	// operations
	opts.OpenTelemetry.TrackConnections = TrackLevel.Normal;


	// This opts into exporting a counter just on the number
	// of events being appended. Kinda a duplication
	opts.OpenTelemetry.TrackEventCounters();

}).UseLightweightSessions();

if (builder.Environment.IsDevelopment())
	builder.Services.InitializeMartenWith<CatalogInitialData>();

builder.Services.AddCarter();

builder.Services.AddExceptionHandler<CustomExceptionHandler>();

builder.Services.AddHealthChecks()
	.AddNpgSql(builder.Configuration.GetConnectionString("Database")!);

var app = builder.Build();

app.MapCarter();

app.UseExceptionHandler(options => { });

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHealthChecks("/health",
	new HealthCheckOptions
	{
		ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
	});

app.Run();
