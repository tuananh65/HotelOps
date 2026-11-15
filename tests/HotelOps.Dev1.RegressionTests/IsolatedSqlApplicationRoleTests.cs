using HotelOps.Api.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IsolatedSqlApplicationRoleTests
{
    private const string Safe="Server=localhost;Database=HotelOps_DEV3_142A_fixture;Encrypt=true;Pooling=false;Enlist=false";
    [Theory]
    [InlineData("Pooling=true",true)]
    [InlineData("Enlist=true",true)]
    [InlineData("Encrypt=false",true)]
    [InlineData("Database=HotelOpsDB",true)]
    [InlineData("",false)]
    public void Unsafe_connection_or_non_development_host_cannot_enable_trial(string change,bool development)
        => Assert.Throws<InvalidOperationException>(()=>new IsolatedSqlApplicationRole(Safe+";"+change,"isolated_role",new string('x',40),
            "synthetic-server",Guid.NewGuid(),Guid.NewGuid().ToString("N"),development));

    [Fact]
    public void Partial_configuration_cannot_silently_fall_back_to_login_permissions()
    {
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["IsolatedSqlApplicationRole:Name"]="isolated_role" }).Build();
        Assert.Throws<InvalidOperationException>(()=>IsolatedSqlApplicationRole.FromConfiguration(configuration,new Environment()));
    }
    private sealed class Environment:IHostEnvironment
    {
        public string EnvironmentName{get;set;}="Development";
        public string ApplicationName{get;set;}="Synthetic test";
        public string ContentRootPath{get;set;}="";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider{get;set;}=new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
