using HotelOps.Api.Data;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class RuntimeDiVerification
{
    public static void Run()
    {
        var services = new ServiceCollection();
        services.AddDbContext<HotelOpsDbContext>(options => options.UseSqlServer("Server=unused;Database=unused;Integrated Security=true"));
        services.AddScoped<IWorkTimeTransactionParticipant, SqlWorkTimeTransactionParticipant>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        var participant = scope.ServiceProvider.GetRequiredService<IWorkTimeTransactionParticipant>();
        var db = scope.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
        if (participant is not SqlWorkTimeTransactionParticipant || db.Model.FindEntityType(typeof(HotelOps.Api.Entities.Workday)) is null)
            throw new InvalidOperationException("DI/model failed");
        Console.WriteLine($"PASS: actual scoped SQL participant graph resolves without SQL; EF model contains {db.Model.GetEntityTypes().Count()} entity types. Program.cs insertion is checked separately by the preservation manifest.");
    }
}
