# Implementation

StartupSchemaValidator uses the scoped configured HotelOpsDbContext connection with raw asynchronous ADO metadata calls. The context owns/disposes the connection. No User entity is materialized. There is one SELECT, no writes and no dependency on fixture database names or local server configuration.

Results are Compatible, MissingRequiredSchema, DatabaseInaccessible and UnexpectedValidationError. Open failures and recognized transport/timeout errors classify inaccessible; malformed configuration or unexpected query errors classify unexpected. Metadata returning no match classifies missing. Every non-compatible result stops startup. Caller cancellation propagates; a linked 30-second budget bounds SQL open/query, with a 15-second command timeout. The budget is not an overall process-launch SLA or a replacement for orchestrator timeouts.

Provider messages/inner exceptions/connection strings are not logged or attached to the public startup failure. Raw ADO calls avoid EF connection/command-error logging for this probe. Program throws only a constant message plus enum category. Other existing infrastructure remains responsible for its own logging policy.

## Exact Program.cs addition relative to Task 26

```csharp
builder.Services.AddScoped<StartupSchemaValidator>();
```

Immediately after builder.Build(), before all middleware registration and app.Run():

```csharp
await using (var schemaScope = app.Services.CreateAsyncScope())
{
    var compatibility = await schemaScope.ServiceProvider.GetRequiredService<StartupSchemaValidator>()
        .ValidateAsync(app.Lifetime.ApplicationStopping);
    if (compatibility != SchemaCompatibility.Compatible)
        throw new InvalidOperationException($"Startup schema validation failed: {compatibility}. HTTP listener was not started.");
}
```

Task 26's two profile registrations remain intact. No middleware is moved; no health response changes; no changes to DEV2 services or authentication/authorization. Because no listener starts until app.Run(), incompatible databases cannot receive business traffic. This is a startup check, not ongoing schema-drift monitoring: prohibit concurrent DDL during service operation.
