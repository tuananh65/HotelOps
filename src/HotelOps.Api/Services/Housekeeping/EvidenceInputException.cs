using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.Services.Housekeeping;

// Input-policy failures only. Storage/IO and commit failures retain their existing handling.
public sealed class EvidenceInputException(string message) : ValidationException(message);
