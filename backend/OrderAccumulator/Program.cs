using System.Text.Json.Serialization;
using OrderAccumulator.Endpoints;
using OrderAccumulator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<ExposureService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.PropertyNameCaseInsensitive = false;
});
builder.Services.AddCors(options => options.AddPolicy("OrderGenerator", policy => policy
    .WithOrigins("http://localhost:5173")
    .WithMethods("POST")
    .AllowAnyHeader()));

var app = builder.Build();

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

app.MapOrderEndpoints();
app.Run();

public partial class Program { }
