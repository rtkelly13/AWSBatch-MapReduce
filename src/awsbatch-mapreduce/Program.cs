// See https://aka.ms/new-console-template for more information

using awsbatch_mapreduce;
using Serilog;

using var log = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
Log.Logger = log;


return await Implementation.SetupJob();