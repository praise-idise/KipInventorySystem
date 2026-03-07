using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace KipInventorySystem.Application.Services.Inventory.Common;

public static class InventoryTelemetry
{
    public const string MeterName = "KipInventorySystem.Inventory";
    public const string ActivitySourceName = "KipInventorySystem.Inventory";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> CommandCounter = Meter.CreateCounter<long>("inventory.commands.total");
    public static readonly Counter<long> CommandFailureCounter = Meter.CreateCounter<long>("inventory.commands.failures");
    public static readonly Counter<long> CommandRetryCounter = Meter.CreateCounter<long>("inventory.commands.retries");
    public static readonly Histogram<double> CommandDurationMs = Meter.CreateHistogram<double>("inventory.commands.duration.ms");
}
