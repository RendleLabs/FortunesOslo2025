using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Fortunes;

internal static class Telemetry
{
    public static readonly ActivitySource ActivitySource = new("Fortunes");
    
    public static readonly Meter Meter = new("Fortunes");
    
    public static readonly Histogram<int> FortuneSize = Meter.CreateHistogram<int>("ndc.fortune_size", "characters");
}