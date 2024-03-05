using OpAMPDemo;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("logging.json", optional: false, reloadOnChange: true);
builder.Configuration.Add<OpAmpConfigSource>(src =>
{
    src.ServiceName = "serviceA";
});
builder.Services.AddOpenTelemetry().WithTracing(tracing => {
    tracing.AddAspNetCoreInstrumentation();
    tracing.AddConsoleExporter();
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var provider = new LogCategoryProvider();
builder.Services.AddLogging(logging =>
{
    logging.AddProvider(provider);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();

app.Run();
