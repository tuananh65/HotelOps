using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "HotelOps.Permission:Global:USER_MANAGE")]
[Authorize(Policy = "HotelOps.Permission:Global:ROLE_MANAGE")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UserAccountCreationController(UserAccountCreationService accounts) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateEmployeeAccountResponse>> Create([FromBody] JsonElement body, CancellationToken ct)
    {
        try
        {
            using var result = await accounts.CreateAsync(CreateEmployeeAccountRequest.Parse(body), ct);
            return result.Status switch
            {
                AccountCreationStatus.Success => Created($"/api/users/{result.User!.Id}",
                    new CreateEmployeeAccountResponse(result.User,
                        new InitialCredentialHandoffDto(result.Credential!.TakePasswordOnce(), result.Credential.ExpiresAtUtc))),
                AccountCreationStatus.Forbidden => Forbid(),
                AccountCreationStatus.InvalidReference => Problem(statusCode: 400, title: "Invalid active branch, department or initial role relationship"),
                AccountCreationStatus.Duplicate => Problem(statusCode: 409, title: "Employee code, username or email already exists",
                    extensions: new Dictionary<string, object?> { ["duplicateFields"] = result.DuplicateFields }),
                _ => throw new InvalidOperationException("Unknown account creation outcome.")
            };
        }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "Account validation failed", detail: error.Message); }
    }
}
