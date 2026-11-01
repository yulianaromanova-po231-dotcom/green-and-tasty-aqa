using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace APIHandler.Models.GroupReservation;

/// <summary>
/// Body of POST /group-reservations/{groupReservationId}/submit.
/// </summary>
public record SubmitGroupPreOrderRequest(
    [property: Required(AllowEmptyStrings = false)]
    [property: JsonPropertyName("reservationId")] string ReservationId);
