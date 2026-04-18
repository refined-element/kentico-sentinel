using RefinedElement.Kentico.Sentinel.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("sentinel");
    config.SetApplicationVersion("0.1.0-alpha");

    config.AddCommand<ScanCommand>("scan")
        .WithDescription("Scan an Xperience by Kentico project for health issues.")
        .WithExample(["scan", "--path", "./MySite"])
        .WithExample(["scan", "--path", ".", "--connection-string", "Server=...;Database=..."]);

    config.AddCommand<QuoteCommand>("quote")
        .WithDescription("Email a sanitized scan report to Refined Element for a remediation quote.")
        .WithExample(["quote", "--report", "./sentinel-report.json"]);
});

return await app.RunAsync(args);
