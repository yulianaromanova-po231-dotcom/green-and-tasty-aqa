using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Contexts;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

/// <summary>A participant as the feature files describe one: dishes as "2 x Caesar, 1 x Tom Yum" in a stable order.</summary>
public sealed record ParticipantView(string Name, string Type, string Dishes, decimal Total, bool CartConfirmed);

/// <summary>The group pre-order as the owner was shown it by the last request (a review or a removal).</summary>
public sealed record GroupPreOrderView(IReadOnlyList<ParticipantView> Participants, decimal GrandTotal, string Body);

public sealed class TheGroupPreOrderShown : IQuestion<GroupPreOrderView>
{
    public Task<GroupPreOrderView> AnsweredByAsync(Actor actor)
    {
        var response = actor.TheLastResponse();
        var snapshot = response.Json<GroupSessionsResponse>();
        var menu = actor.Recall<Menu>();

        var participants = snapshot.Participants
            .Select(p => new ParticipantView(
                p.Name,
                p.Type,
                DishList.Describe(p.Items.Select(item => (menu.AliasOf(item.Dish.Id), item.Quantity))),
                p.Total,
                p.IsSubmitted))
            .ToList();

        return Task.FromResult(new GroupPreOrderView(participants, snapshot.Total, response.Body));
    }
}

/// <summary>The consolidated list the restaurant receives, as returned by the last (successful) submit.</summary>
public sealed class TheConsolidatedPreOrder : IQuestion<IReadOnlyDictionary<string, int>>
{
    public Task<IReadOnlyDictionary<string, int>> AnsweredByAsync(Actor actor)
    {
        var menu = actor.Recall<Menu>();
        var lines = actor.TheLastResponse().Json<List<ConsolidatedItemResponse>>();
        IReadOnlyDictionary<string, int> answer = lines.ToDictionary(line => menu.AliasOf(line.DishId), line => line.Quantity);
        return Task.FromResult(answer);
    }
}

/// <summary>The status of the owner's group pre-order ("Open", "Submitted", "Closed"), fetched now.</summary>
public sealed class TheGroupStatus : IQuestion<string>
{
    public async Task<string> AnsweredByAsync(Actor actor)
    {
        var invite = actor.Recall<GroupInvite>(Personas.Owner);
        var owner = actor.Recall<CustomerAccount>(Personas.Owner);
        var response = new ApiResponseContext();
        await response.CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi()).GetStatusAsync(invite.GroupReservationId, owner.IdToken));
        return response.Json<GroupStatusResponse>().Status;
    }
}

/// <summary>The pre-order of the owner's booking as a requester (the owner or the waiter) sees it, fetched now.</summary>
public sealed record ReservationOrderView(IReadOnlyDictionary<string, int> Dishes, OrderStatus Status, string Body);

public sealed class TheReservationOrder(Requester requester) : IQuestion<ReservationOrderView>
{
    public async Task<ReservationOrderView> AnsweredByAsync(Actor actor)
    {
        var invite = actor.Recall<GroupInvite>(Personas.Owner);
        var menu = actor.Recall<Menu>();
        var token = await actor.AsksForAsync(new TheCredentialsOf(requester));
        var response = new ApiResponseContext();
        await response.CaptureAsync(await new OrdersApi(actor.UsingTheApi()).GetOrderAsync(invite.ReservationId, token));

        var order = response.Json<GetOrderResponse>();
        var dishes = order.OrderItems
            .GroupBy(item => menu.AliasOf(item.Dish.Id))
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        return new ReservationOrderView(dishes, order.Status, response.Body);
    }
}

/// <summary>Turns dish lists into the "2 x Caesar, 1 x Tom Yum" wording of the feature files and back.</summary>
public static class DishList
{
    public static string Describe(IEnumerable<(string Dish, int Quantity)> items) =>
        string.Join(", ", items.OrderBy(item => item.Dish, StringComparer.Ordinal).Select(item => $"{item.Quantity} x {item.Dish}"));

    public static IReadOnlyList<(string Dish, int Quantity)> Parse(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part =>
                {
                    var pieces = part.Split(" x ", 2, StringSplitOptions.TrimEntries);
                    return pieces.Length == 2 && int.TryParse(pieces[0], out var quantity)
                        ? (pieces[1], quantity)
                        : throw new FormatException($"Expected '<quantity> x <dish>', got '{part}'.");
                })
                .ToList();

    public static string Normalize(string? text) => Describe(Parse(text));
}
