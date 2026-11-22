// A standalone build helper, not part of the API or its solution. Input/output are private pipes.
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
try
{
    using var input = JsonDocument.Parse(await Console.In.ReadToEndAsync());
    var password = input.RootElement.GetProperty("password").GetString()!;
    var hasher = new PasswordHasher<object>(Options.Create(new PasswordHasherOptions { IterationCount = 210000 }));
    var result = input.RootElement.GetProperty("usernames").EnumerateArray().Select(x =>
    {
        var hash = hasher.HashPassword(new object(), password);
        if (hasher.VerifyHashedPassword(new object(), hash, password) != PasswordVerificationResult.Success)
            throw new InvalidOperationException();
        return new { username = x.GetString(), password_hash = hash };
    }).ToArray();
    Console.Write(JsonSerializer.Serialize(result));
}
catch { Console.Error.WriteLine("Credential generation failed; details suppressed."); Environment.ExitCode = 1; }
