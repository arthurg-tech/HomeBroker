using System.Text.Json.Serialization;
using OrderAccumulator.Endpoints;
using OrderAccumulator.RateLimiting;
using OrderAccumulator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<ExposureService>();
builder.Services.AddOptions<OrderRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(OrderRateLimitOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.SegmentsPerWindow <= options.WindowSeconds,
        "Cada segmento da janela de rate limiting deve ter pelo menos um segundo.")
    .ValidateOnStart();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy<string, OrderRateLimitPolicy>(OrderRateLimitPolicy.PolicyName);
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.PropertyNameCaseInsensitive = false;
});
builder.Services.AddCors(options => options.AddPolicy("OrderGenerator", policy => policy
    .WithOrigins("http://localhost:5173")
    .WithMethods("POST")
    .WithExposedHeaders("Retry-After")
    .AllowAnyHeader()));

var app = builder.Build();

app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseCors("OrderGenerator");
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "OrderAccumulator v1");
        options.DocumentTitle = "OrderAccumulator — Swagger UI";
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.MapOrderEndpoints();
app.Run();

public partial class Program { }
