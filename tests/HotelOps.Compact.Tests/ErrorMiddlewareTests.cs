using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class ErrorMiddlewareTests
{
    [Theory]
    [InlineData(1205,409,"TRANSACTION_RETRY_REQUIRED")]
    [InlineData(2627,409,"DUPLICATE_RECORD")]
    [InlineData(547,409,"DATABASE_INVARIANT_VIOLATION")]
    [InlineData(8152,400,"INVALID_FIELD_VALUE")]
    [InlineData(99999,500,"INTERNAL_ERROR")]
    public async Task WrappedSqlFailureUsesPublicContractWithoutRetry(int number,int status,string code)
    {
        var failure=new InvalidOperationException("EF transient failure",
            new DbUpdateException("Write failed",SqlFailure(number)));
        await AssertResponse(failure,status,code);
    }

    [Fact]
    public Task OrdinaryInvalidOperationIsNotClassifiedAsDeadlock()=>
        AssertResponse(new InvalidOperationException("1205 is only text"),500,"INTERNAL_ERROR");

    [Fact]
    public Task BusinessFailureKeepsItsContract()=>
        AssertResponse(new ApiException(403,"ACTION_NOT_ALLOWED"),403,"ACTION_NOT_ALLOWED");

    private static async Task AssertResponse(Exception failure,int status,string code)
    {
        using var services=new ServiceCollection().AddOptions().BuildServiceProvider();
        var context=new DefaultHttpContext { RequestServices=services };
        await using var body=new MemoryStream();context.Response.Body=body;
        var calls=0;
        var middleware=new ErrorMiddleware(_=>{calls++;return Task.FromException(failure);},NullLogger<ErrorMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(1,calls);
        Assert.Equal(status,context.Response.StatusCode);
        body.Position=0;
        using var json=await JsonDocument.ParseAsync(body);
        Assert.Equal(code,json.RootElement.GetProperty("title").GetString());
        Assert.Equal(status,json.RootElement.GetProperty("status").GetInt32());
        Assert.False(json.RootElement.TryGetProperty("detail",out _));
    }

    // SqlClient exposes no public constructor. Build the real exception without a database connection.
    private static SqlException SqlFailure(int number)
    {
        var ctor=typeof(SqlError).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic)
            .Where(c=>c.GetParameters().Length>=8).OrderBy(c=>c.GetParameters().Length).First();
        var args=ctor.GetParameters().Select(p=>p.ParameterType.IsValueType?Activator.CreateInstance(p.ParameterType):null).ToArray();
        args[0]=number;args[1]=(byte)1;args[2]=(byte)13;args[3]="TEST";
        args[4]="Synthetic SQL failure";args[5]="test";args[6]=1;
        var error=(SqlError)ctor.Invoke(args);
        var errors=(SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection),true)!;
        typeof(SqlErrorCollection).GetMethod("Add",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(errors,[error]);
        return (SqlException)typeof(SqlException).GetMethod("CreateException",BindingFlags.Static|BindingFlags.NonPublic,
            null,[typeof(SqlErrorCollection),typeof(string)],null)!.Invoke(null,[errors,"test"])!;
    }
}
