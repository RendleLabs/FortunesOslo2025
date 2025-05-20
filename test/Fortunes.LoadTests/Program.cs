using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

using var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri("https://localhost:7261");

var randomFortuneScenario = CreateRandomFortuneScenario(httpClient, "Get Random Fortune");
var fortuneScenario = CreateFortuneScenario(httpClient, "Get Fortune");

NBomberRunner.RegisterScenarios(fortuneScenario)
    .Run();

static ScenarioProps CreateRandomFortuneScenario(HttpClient httpClient, string name)
{
    var props = Scenario.Create(name, async context =>
    {
        var request = Http.CreateRequest("GET", "/fortune")
            .WithHeader("Accept", "application/json");
        
        var response = await Http.Send(httpClient, request);
        
        return response.IsError
            ? Response.Fail()
            : Response.Ok(statusCode: response.StatusCode, sizeBytes: response.SizeBytes);
    })
    .WithLoadSimulations(
        Simulation.Inject(rate: 100,
            interval: TimeSpan.FromSeconds(1),
            during: TimeSpan.FromSeconds(60))
        );

    return props;
}
static ScenarioProps CreateFortuneScenario(HttpClient httpClient, string name)
{
    var props = Scenario.Create(name, async context =>
        {
            var id = Random.Shared.Next(6000);
            var request = Http.CreateRequest("GET", $"/fortune/{id}")
                .WithHeader("Accept", "application/json");
        
            var response = await Http.Send(httpClient, request);
        
            return response.IsError
                ? Response.Fail()
                : Response.Ok(statusCode: response.StatusCode, sizeBytes: response.SizeBytes);
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 100,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(60))
        );

    return props;
}
