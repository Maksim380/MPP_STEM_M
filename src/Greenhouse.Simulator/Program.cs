using Greenhouse.Simulator;

SimulatorOptions options;
try
{
    options = SimulatorOptions.Parse(args);
}
catch (ArgumentException e)
{
    await Console.Error.WriteLineAsync(e.Message);
    await Console.Error.WriteLineAsync(SimulatorOptions.Usage);
    return 2;
}

if (options.ShowHelp)
{
    Console.WriteLine(SimulatorOptions.Usage);
    return 0;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
var runner = new SimulatorRunner(http, options, Console.Out, TimeProvider.System);
await runner.RunAsync(cts.Token);
Console.WriteLine("Симулятор зупинено.");
return 0;
