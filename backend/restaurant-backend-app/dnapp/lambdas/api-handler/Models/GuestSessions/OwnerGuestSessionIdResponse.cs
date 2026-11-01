using System.Text.Json.Serialization;

namespace APIHandler.Models.GuestSessions;

/// <summary>
/// Response of GET /reservations/{reservationId}/guest-session-id.
/// </summary>
public record OwnerGuestSessionIdResponse(
    [property: JsonPropertyName("guestSessionId")] string GuestSessionId);
