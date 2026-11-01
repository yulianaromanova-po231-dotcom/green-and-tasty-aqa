using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Ids the feature files never spell out: what to use when a scenario asks for something that does not exist.</summary>
public static class Missing
{
    public static string Id() => $"aqa-missing-{Guid.NewGuid():N}";
}

/// <summary>Asks for the group pre-order of the owner's booking (or of a group that does not exist) on behalf of a
/// requester. The answer is left as the scenario's last response.</summary>
public sealed class ReviewTheGroupPreOrder(Requester requester = Requester.Owner, bool ofAMissingGroup = false) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var groupId = ofAMissingGroup ? Missing.Id() : actor.Recall<GroupInvite>(Personas.Owner).GroupReservationId;
        var token = await actor.AsksForAsync(new TheCredentialsOf(requester));
        await actor.TheLastResponse().CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi()).GetSessionsAsync(groupId, token));
    }
}

/// <summary>What a submit points at: the owner's own group and booking, or something that is not there.</summary>
public enum SubmitTarget
{
    TheOwnersGroup,
    AMissingGroup,
    AMissingBooking,
}

/// <summary>Submits the consolidated group pre-order on behalf of a requester; the answer is the scenario's last response.</summary>
public sealed class SubmitTheGroupPreOrder(Requester requester = Requester.Owner, SubmitTarget target = SubmitTarget.TheOwnersGroup) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var invite = actor.Recall<GroupInvite>(Personas.Owner);
        var groupId = target == SubmitTarget.AMissingGroup ? Missing.Id() : invite.GroupReservationId;
        var reservationId = target == SubmitTarget.AMissingBooking ? Missing.Id() : invite.ReservationId;
        var token = await actor.AsksForAsync(new TheCredentialsOf(requester));

        await actor.TheLastResponse().CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi())
            .SubmitGroupPreOrderAsync(groupId, new SubmitGroupPreOrderRequest(reservationId), token));
    }
}

/// <summary>Who is being removed from the owner's group pre-order.</summary>
public enum RemovalTarget
{
    /// <summary>A guest of the owner's group, by name.</summary>
    Guest,

    /// <summary>A guest who joined another owner's group.</summary>
    GuestOfAnotherGroup,

    /// <summary>A participant that never existed.</summary>
    Nobody,

    /// <summary>The owner's own session in the group.</summary>
    TheOwnersOwnSession,
}

/// <summary>Removes a participant from the owner's group pre-order on behalf of a requester. The answer — the updated
/// list of participants when it works — is the scenario's last response.</summary>
public sealed class RemoveTheParticipant(RemovalTarget target, string? guestName = null, Requester requester = Requester.Owner) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var groups = new GroupOrdersApi(actor.UsingTheApi());
        var invite = actor.Recall<GroupInvite>(Personas.Owner);
        var ownerToken = actor.Recall<CustomerAccount>(Personas.Owner).IdToken;

        string sessionId;
        switch (target)
        {
            case RemovalTarget.Guest:
            case RemovalTarget.GuestOfAnotherGroup:
                sessionId = actor.Recall<GuestAccount>(guestName ?? throw new InvalidOperationException("Name the guest to remove.")).SessionId;
                break;
            case RemovalTarget.Nobody:
                sessionId = Missing.Id();
                break;
            case RemovalTarget.TheOwnersOwnSession:
                var owner = await groups.GetOwnerSessionIdAsync(invite.ReservationId, ownerToken);
                var ownerSession = actor.TheLastResponse();
                await ownerSession.CaptureAsync(owner);
                sessionId = ownerSession.Json<OwnerSessionIdResponse>().GuestSessionId;
                break;
            default:
                throw new InvalidOperationException($"Unknown removal target {target}.");
        }

        var token = await actor.AsksForAsync(new TheCredentialsOf(requester));
        await actor.TheLastResponse().CaptureAsync(await groups.RemoveParticipantAsync(invite.GroupReservationId, sessionId, token));
    }
}
