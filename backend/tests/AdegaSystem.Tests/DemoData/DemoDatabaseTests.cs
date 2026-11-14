using AdegaSystem.Api.Data.DemoData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AdegaSystem.Tests.DemoData;

/// <summary>As travas que impedem os comandos da demo de usar outro banco ou outro ambiente.</summary>
public class DemoDatabaseTests
{
    private const string Demo = "Server=localhost;Port=3307;Database=adega_demo;User=adega;Password=x;";
    private const string Normal = "Server=localhost;Port=3307;Database=adega;User=adega;Password=x;";

    [Theory]
    [InlineData(new string[0], DemoMode.None)]
    [InlineData(new[] { "seed-demo" }, DemoMode.Seed)]
    [InlineData(new[] { "--urls", "http://localhost:5000", "use-demo" }, DemoMode.Use)]
    [InlineData(new[] { "SEED-DEMO" }, DemoMode.Seed)]
    public void ParseMode_ReadsTheCommandLine(string[] args, DemoMode expected)
    {
        Assert.Equal(expected, DemoDatabase.ParseMode(args));
    }

    [Fact]
    public void GetConnectionString_InDevelopment_ReturnsTheDemoConnection()
    {
        Assert.Equal(Demo, DemoDatabase.GetConnectionString(Configuration(Demo, Normal), Env("Development")));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void GetConnectionString_OutsideDevelopment_Refuses(string environment)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DemoDatabase.GetConnectionString(Configuration(Demo, Normal), Env(environment)));
        Assert.Contains("Development", ex.Message);
    }

    [Fact]
    public void GetConnectionString_WithoutDemoConnection_RefusesInsteadOfUsingTheNormalDatabase()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DemoDatabase.GetConnectionString(Configuration(null, Normal), Env("Development")));
        Assert.Contains("DemoConnection", ex.Message);
    }

    [Theory]
    [InlineData("Server=localhost;Port=3307;Database=adega;User=adega;Password=x;")]
    [InlineData("Server=localhost;Port=3307;Database=adega_demo2;User=adega;Password=x;")]
    [InlineData("Server=localhost;Port=3307;User=adega;Password=x;")]
    public void GetConnectionString_PointingToAnotherDatabase_Refuses(string demoConnection)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DemoDatabase.GetConnectionString(Configuration(demoConnection, Normal), Env("Development")));
        Assert.Contains("adega_demo", ex.Message);
    }

    [Fact]
    public void GetConnectionString_WhenTheNormalConnectionIsAlsoTheDemoDatabase_Refuses()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DemoDatabase.GetConnectionString(Configuration(Demo, Demo), Env("Development")));
        Assert.Contains("DefaultConnection", ex.Message);
    }

    private static IConfiguration Configuration(string? demo, string? normal) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DemoConnection"] = demo,
                ["ConnectionStrings:DefaultConnection"] = normal,
            })
            .Build();

    private static IHostEnvironment Env(string name) => new TestEnvironment(name);

    /// <summary>Ambiente de mentira: só o nome importa para as travas.</summary>
    public sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "AdegaSystem.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
