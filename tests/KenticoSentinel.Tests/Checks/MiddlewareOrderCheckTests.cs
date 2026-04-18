using KenticoSentinel.Tests.Support;
using RefinedElement.Kentico.Sentinel.Checks.Configuration;
using RefinedElement.Kentico.Sentinel.Core;

namespace KenticoSentinel.Tests.Checks;

public class MiddlewareOrderCheckTests
{
    private static ScanContext Ctx(string repoPath) => new()
    {
        RepoPath = repoPath,
        HttpClientFactory = new FakeHttpClientFactory(),
    };

    [Fact]
    public async Task Correct_trio_order_produces_no_findings()
    {
        using var repo = new TempRepo();
        repo.Write("Program.cs", """
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddKentico();
            var app = builder.Build();
            app.InitKentico();
            app.UseStaticFiles();
            app.UseKentico();
            app.UseWebOptimizer();
            app.Run();
            """);

        var findings = await new MiddlewareOrderCheck().RunAsync(Ctx(repo.Path), CancellationToken.None);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task UseWebOptimizer_before_UseKentico_is_error()
    {
        using var repo = new TempRepo();
        repo.Write("Program.cs", """
            var app = builder.Build();
            app.InitKentico();
            app.UseWebOptimizer();
            app.UseStaticFiles();
            app.UseKentico();
            """);

        var findings = await new MiddlewareOrderCheck().RunAsync(Ctx(repo.Path), CancellationToken.None);

        Assert.Contains(findings, f => f.Severity == Severity.Error && f.Message.Contains("UseWebOptimizer"));
    }

    [Fact]
    public async Task Middleware_between_trio_is_error()
    {
        using var repo = new TempRepo();
        repo.Write("Program.cs", """
            var app = builder.Build();
            app.InitKentico();
            app.UseAuthentication();
            app.UseStaticFiles();
            app.UseKentico();
            """);

        var findings = await new MiddlewareOrderCheck().RunAsync(Ctx(repo.Path), CancellationToken.None);

        Assert.Contains(findings, f => f.Severity == Severity.Error && f.Message.Contains("contiguous"));
    }
}
