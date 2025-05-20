using System.Diagnostics;
using System.Text.Json.Serialization;
using EFCore.BulkExtensions;
using Fortunes;
using Fortunes.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        resource.Clear()
            .AddService("fortunes")
            .AddEnvironmentVariableDetector()
            .AddTelemetrySdk()
            .AddAttributes([new KeyValuePair<string, object>("workshop.location", "Oslo")]);
    })
    .WithLogging()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Fortunes")
            .AddAspNetCoreInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddNpgsql();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddNpgsqlInstrumentation();
    })
    .UseOtlpExporter();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddDbContextPool<FortuneContext>(db =>
    db.UseNpgsql(builder.Configuration.GetConnectionString("Fortunes"))
        .UseSeeding((context, b) =>
        {
            if (context.Set<Fortune>().Any()) return;
            
            var fortunes = FortuneSource.Get()
                .Select(t => new Fortune { Text = t });
            
            context.BulkInsert(fortunes);
        })
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
    );

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    using (var context = scope.ServiceProvider.GetRequiredService<FortuneContext>())
    {
        context.Database.Migrate();
    }
}

app.UseHttpsRedirection();

app.MapGet("/fortune", (FortuneContext context) =>
{
    var activity = Activity.Current;
    var fortune = context.Fortunes.OrderBy(f => EF.Functions.Random()).First();
    Telemetry.FortuneSize.Record(fortune.Text.Length);
    if (activity is not null)
    {
        activity.SetTag("ndc.fortune.text", fortune.Text);
        activity.SetTag("ndc.fortune.id", fortune.Id);
    }
    return fortune;
});

app.MapGet("/fortune/{id}", async (FortuneContext context, int id) =>
{
    var activity = Activity.Current;
    activity?.SetTag("ndc.fortune.id", id);
    
    try
    {
        var fortune = await context.Fortunes.FirstOrDefaultAsync(f => f.Id == id);
        if (fortune is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            return Results.NotFound();
        }
        Telemetry.FortuneSize.Record(fortune.Text.Length);
        if (activity is not null)
        {
            activity.SetTag("ndc.fortune.text", fortune.Text);
        }
        return Results.Ok(fortune);
    }
    catch (Exception e)
    {
        if (activity is not null)
        {
            activity.SetStatus(ActivityStatusCode.Error);
            activity.AddException(e);
        }
        return Results.InternalServerError();
    }
});

Console.WriteLine("Running...");
app.Run();

[JsonSerializable(typeof(Fortune))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}
