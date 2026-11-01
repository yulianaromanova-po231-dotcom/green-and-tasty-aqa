using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>The owner pre-orders their own dishes for the booking. This creates the order the consolidated group
/// pre-order is added to; without it the group pre-order cannot be submitted.</summary>
public sealed class PlaceOwnPreOrder(IReadOnlyList<(string Dish, int Quantity)> items, string persona = Personas.Owner) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var owner = actor.Recall<CustomerAccount>(persona);
        var reservation = actor.Recall<ReservationResponse>(persona);
        var menu = actor.Recall<Menu>();
        var response = actor.TheLastResponse();

        var request = new CreateOrderRequest(reservation.Id, items.Select(item => new OrderItemRequest(menu[item.Dish].Id, item.Quantity)).ToList());
        await response.CaptureAsync(await new OrdersApi(actor.UsingTheApi()).CreateOrderAsync(request, owner.IdToken));
        if (!response.Succeeded)
            throw new InvalidOperationException($"The owner could not pre-order their own dishes: {response.Status} {response.Body}");
    }
}
