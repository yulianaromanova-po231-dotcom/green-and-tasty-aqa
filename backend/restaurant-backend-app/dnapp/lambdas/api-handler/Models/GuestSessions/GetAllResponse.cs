using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIHandler.Models.GuestSessions;

/// <summary>
/// Snapshot of a group pre-order as seen by the reservation owner:
/// every participant session with its dishes and totals, plus the grand total.
/// Returned by GET and DELETE /guest/groups/{groupReservationId}/sessions.
/// </summary>
public record GetAllSessionsResponse(
    [property: JsonPropertyName("participants")] IEnumerable<Participant> Participants,
    [property: JsonPropertyName("total")] decimal Total);

public record Participant(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("type")]
    [property: JsonConverter(typeof(JsonStringEnumConverter<ParticipantType>))] ParticipantType Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("items")] IEnumerable<GroupSessionItemResponse> Items,
    [property: JsonPropertyName("total")] decimal Total,
    [property: JsonPropertyName("isSubmitted")] bool IsSubmitted);

public record GroupSessionItemResponse(
    [property: JsonPropertyName("dish")] GroupSessionDishResponse Dish,
    [property: JsonPropertyName("quantity")] int Quantity);

public record GroupSessionDishResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("price")] decimal Price);

public enum ParticipantType
{
    Owner,
    Guest
}
