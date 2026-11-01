using System.Text.Json.Serialization;

namespace APIHandler.Models.GroupReservation;

/// <summary>
/// One line of the consolidated group pre-order: a dish and the summed quantity
/// across all submitted guest sessions.
/// </summary>
public record ConsolidatedOrderItemResponse(
    [property: JsonPropertyName("dishId")] string DishId,
    [property: JsonPropertyName("quantity")] int Quantity);
