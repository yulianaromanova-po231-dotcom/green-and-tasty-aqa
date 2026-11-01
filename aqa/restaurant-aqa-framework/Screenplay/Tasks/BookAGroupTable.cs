using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>
/// The customer (signed up on the spot if the scenario has not done it yet) books the scenario's table for a group
/// and fetches the invitation. A booking for two or more guests opens a group pre-order. The time is always in the
/// future (<c>now + days</c>, at a fixed hour per booking), so the scenario never depends on the clock. The booking is
/// cancelled afterwards, which also closes its group.
/// </summary>
public sealed class BookAGroupTable(string persona, int guests, int hourOffset = 0) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        if (!HasPersona(actor))
            await actor.AttemptsToAsync(new SignUpAsCustomer(persona));

        var customer = actor.Recall<CustomerAccount>(persona);
        var table = actor.Recall<TableResponse>();
        var api = actor.UsingTheApi();
        var response = actor.TheLastResponse();

        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(10 + (2 * hourOffset));
        var reservations = new ReservationsApi(api);
        await response.CaptureAsync(await reservations.CreateReservationAsync(
            SampleDataFactory.CreateReservationRequest(table.Id, start, guests), customer.IdToken));
        if (!response.Succeeded)
            throw new InvalidOperationException($"Booking the table failed with {response.Status}: {response.Body}");

        var reservation = response.Json<ReservationResponse>();
        actor.Remember(persona, reservation);
        actor.Recall<CleanupScope>().TrackDelete(
            $"reservation:{reservation.Id}",
            () => reservations.CancelReservationAsync(reservation.Id, customer.IdToken));

        var groups = new GroupOrdersApi(api);
        await response.CaptureAsync(await groups.GetInviteTokenAsync(reservation.Id, customer.IdToken));
        if (!response.Succeeded)
            throw new InvalidOperationException($"Getting the invitation failed with {response.Status}: {response.Body}");
        var token = response.Json<InviteTokenResponse>().Token;

        await response.CaptureAsync(await groups.GetInviteAsync(token));
        if (!response.Succeeded)
            throw new InvalidOperationException($"Opening the invitation failed with {response.Status}: {response.Body}");

        actor.Remember(persona, new GroupInvite(reservation.Id, token, response.Json<GroupInviteResponse>().GroupReservationId));
    }

    private bool HasPersona(Actor actor) =>
        actor.RecallAll<CustomerAccount>().Any(account => account.Persona == persona);
}
