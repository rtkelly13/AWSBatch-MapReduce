// See https://aka.ms/new-console-template for more information

using awsbatch_mapreduce;
using CommandLine;
using Serilog;

using var log = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
Log.Logger = log;

var parserResult = await Parser.Default.ParseArguments<SetupConfig, MapConfig, ReduceConfig>
    (args).WithParsedAsync(async opts =>
{
    var exitCode = opts switch
    {
        SetupConfig sc => await Implementation.SetupJob(sc),
        MapConfig mc => await Implementation.MapJob(mc),
        ReduceConfig rc => await Implementation.ReduceJob(rc),
        _ => throw new ArgumentException()
    };
    Environment.ExitCode = exitCode;
});

parserResult.WithNotParsed(err => { Console.WriteLine($"Errors: {string.Join(",", err)}"); });