using Discount.Grpc.Data;
using Discount.Grpc.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Serilog;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddGrpc().AddJsonTranscoding();
builder.Services.AddDbContext<DiscountContext>(opts =>
		opts.UseSqlite(builder.Configuration.GetConnectionString("Database")));

builder.Host.UseSerilog((context, config) =>
{
	config
		.ReadFrom.Configuration(context.Configuration); // Read from appsettings.json;
});

//add json transcodingon the gRPC service
builder.Services.AddGrpcSwagger();
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1",
		new OpenApiInfo { Title = "gRPC transcoding", Version = "v1" });
});

// Configure OpenTelemetry Tracing
builder.Services.AddOpenTelemetry()
	.WithTracing(tracerProviderBuilder =>
	{
		tracerProviderBuilder
			.AddGrpcCoreInstrumentation()  // Capture incoming Grpc requests
			.AddHttpClientInstrumentation() // Capture outgoing HTTP requests
			.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Discount.Grpc"))
			.AddOtlpExporter(options =>
			{
				options.Endpoint = new Uri(builder.Configuration.GetValue<string>("OtelTrace:Endpoint")!);
			});
	})
	.WithMetrics(metrics =>
	{
		metrics.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Discount.Grpc"))
			.AddRuntimeInstrumentation() // Capture .NET runtime metrics
			.AddOtlpExporter(opt =>
			{
				opt.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
				opt.Endpoint = new Uri(builder.Configuration.GetValue<string>("OtelMetric:Endpoint")!);
			});
	});


builder.Services.AddHealthChecks()
	.AddSqlite(builder.Configuration.GetConnectionString("Database")!);

var app = builder.Build();

app.UseHealthChecks("/health",
	new HealthCheckOptions
	{
		ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
	});

app.UseSwagger();
if (app.Environment.IsDevelopment())
{
	app.UseSwaggerUI(c =>
	{
		c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
	});
}

// Configure the HTTP request pipeline.
app.UseMigration();
app.MapGrpcService<DiscountService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
