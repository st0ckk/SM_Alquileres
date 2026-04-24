using SGA.Infrastructure.Middleware;
using SGA.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<ISgaDataService, SgaDataService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClientPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:5070", "https://localhost:7295")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("WebClientPolicy");
app.UseMiddleware<ExceptionLoggingMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
