using Discount.Grpc;
using HealthChecks.UI.Client;
using Marten.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

//Application Services
var assembly = typeof(Program).Assembly;
builder.Services.AddCarter();
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(assembly);
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
    config.AddOpenBehavior(typeof(LoggingBehavior<,>));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Host.UseSerilog((context, config) =>
{
	config
		.ReadFrom.Configuration(context.Configuration); // Read from appsettings.json;
});


//Data Services
builder.Services.AddMarten(opts =>
{
    opts.Connection(builder.Configuration.GetConnectionString("Database")!);
    opts.Schema.For<ShoppingCart>().Identity(x => x.UserName);

	// Turn on Otel tracing for connection activity, and
	// also tag events to each span for all the Marten "write"
	// operations
	opts.OpenTelemetry.TrackConnections = TrackLevel.Normal;


	// This opts into exporting a counter just on the number
	// of events being appended. Kinda a duplication
	opts.OpenTelemetry.TrackEventCounters();

}).UseLightweightSessions();

builder.Services.AddScoped<IBasketRepository, BasketRepository>();
builder.Services.Decorate<IBasketRepository, CachedBasketRepository>();


var redisConnection = ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!);
builder.Services.AddSingleton<IConnectionMultiplexer>(redisConnection);

builder.Services.AddStackExchangeRedisCache(options =>
{
	options.ConnectionMultiplexerFactory = async () => await Task.FromResult(redisConnection);
	//options.InstanceName = "Basket";
});

//Cross-Cutting Services
builder.Services.AddExceptionHandler<CustomExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Database")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);


// Configure OpenTelemetry Tracing
builder.Services.AddOpenTelemetry()
	.WithTracing(tracerProviderBuilder =>
	{
		tracerProviderBuilder
			.AddAspNetCoreInstrumentation()  // Capture incoming HTTP requests
			.AddHttpClientInstrumentation() // Capture outgoing HTTP requests
			.AddRedisInstrumentation(redisConnection)
			.AddSource("Marten")
			.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Basket.API"))
			.AddOtlpExporter(options =>
			{
				options.Endpoint = new Uri(builder.Configuration.GetValue<string>("OtelTrace:Endpoint")!);
			});
	})
	.WithMetrics(metrics =>
	{
		metrics.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Basket.API"))
			.AddAspNetCoreInstrumentation() // Capture request metrics
			.AddRuntimeInstrumentation() // Capture .NET runtime metrics
			.AddMeter("Marten") // Custom application metrics
			.AddOtlpExporter(opt =>
			{
				opt.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
				opt.Endpoint = new Uri(builder.Configuration.GetValue<string>("OtelMetric:Endpoint")!);
			});
	});

//Grpc Services
builder.Services.AddGrpcClient<DiscountProtoService.DiscountProtoServiceClient>(options =>
{
	options.Address = new Uri(builder.Configuration["GrpcSettings:DiscountUrl"]!);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
	var handler = new HttpClientHandler
	{
		ServerCertificateCustomValidationCallback =
		HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
	};

	return handler;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapCarter();
app.UseExceptionHandler(options => { });
app.UseHealthChecks("/health",
    new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.Run();
