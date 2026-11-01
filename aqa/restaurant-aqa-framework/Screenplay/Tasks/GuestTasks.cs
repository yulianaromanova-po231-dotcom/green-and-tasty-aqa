using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>A guest opens the owner's invitation and joins the group pre-order under their name. Guests have no
/// account: the session token they get back is remembered under their name.</summary>
public sealed class JoinTheGroup(string guestName, string ownerPersona = Personas.Owner) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var invite = actor.Recall<GroupInvite>(ownerPersona);
        var response = actor.TheLastResponse();

        await response.CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi())
            .CreateGuestSessionAsync(new CreateGuestSessionRequest(invite.GroupReservationId, guestName)));
        if (!response.Succeeded)
            throw new InvalidOperationException($"{guestName} could not join the group pre-order: {response.Status} {response.Body}");

        var session = response.Json<GuestSessionCreatedResponse>();
        actor.Remember(guestName, new GuestAccount(guestName, session.SessionId, session.SessionToken, invite.GroupReservationId));
    }
}

/// <summary>A guest adds a dish of the scenario's restaurant to their cart.</summary>
public sealed class AddToTheCart(string guestName, string dish, int quantity, bool mustSucceed = true) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var guest = actor.Recall<GuestAccount>(guestName);
        var menu = actor.Recall<Menu>();
        var response = actor.TheLastResponse();

        await response.CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi())
            .AddDishAsync(guest.SessionId, new GuestDishRequest(menu[dish].Id, quantity), guest.Token));
        if (mustSucceed && !response.Succeeded)
            throw new InvalidOperationException($"{guestName} could not add {quantity} x {dish}: {response.Status} {response.Body}");
    }
}

/// <summary>A guest confirms their cart, which makes it part of the group pre-order the owner submits.</summary>
public sealed class ConfirmTheCart(string guestName, bool mustSucceed = true) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var guest = actor.Recall<GuestAccount>(guestName);
        var response = actor.TheLastResponse();

        await response.CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi()).SubmitCartAsync(guest.SessionId, guest.Token));
        if (mustSucceed && !response.Succeeded)
            throw new InvalidOperationException($"{guestName} could not confirm the cart: {response.Status} {response.Body}");
    }
}
