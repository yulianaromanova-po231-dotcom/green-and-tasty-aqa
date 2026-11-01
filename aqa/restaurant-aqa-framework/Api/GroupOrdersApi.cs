using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Api;

/// <summary>
/// The group pre-order: invitations, guest sessions (guest token, no Cognito) and the owner's endpoints
/// (Cognito id token): review the participants, submit the consolidated pre-order, remove a participant.
/// </summary>
public sealed class GroupOrdersApi(IAPIRequestContext api) : ApiClientBase(api)
{
    // ---- owner: invitation ----

    public Task<IAPIResponse> GetInviteTokenAsync(string reservationId, string? bearerToken) =>
        GetAsync(Url($"/reservations/{Uri.EscapeDataString(reservationId)}/invite-token"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> GetInviteAsync(string token) =>
        GetAsync(Url($"/guest/invites/{Uri.EscapeDataString(token)}"));

    public Task<IAPIResponse> GetOwnerSessionIdAsync(string reservationId, string? bearerToken) =>
        GetAsync(Url($"/reservations/{Uri.EscapeDataString(reservationId)}/guest-session-id"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });

    // ---- guest ----

    public Task<IAPIResponse> CreateGuestSessionAsync(CreateGuestSessionRequest request) =>
        Api.PostAsync(Url("/guest/sessions"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(),
        });

    public Task<IAPIResponse> AddDishAsync(string guestSessionId, GuestDishRequest request, string guestToken) =>
        Api.PostAsync(Url($"/guest/sessions/{Uri.EscapeDataString(guestSessionId)}/dishes"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(guestToken),
        });

    public Task<IAPIResponse> SubmitCartAsync(string guestSessionId, string guestToken) =>
        Api.PostAsync(Url($"/guest/sessions/{Uri.EscapeDataString(guestSessionId)}/submit"), new APIRequestContextOptions
        {
            Headers = Headers(guestToken),
        });

    // ---- owner: review, submit, remove ----

    public Task<IAPIResponse> GetSessionsAsync(string groupReservationId, string? bearerToken) =>
        GetAsync(Url($"/guest/groups/{Uri.EscapeDataString(groupReservationId)}/sessions"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> GetStatusAsync(string groupReservationId, string? bearerToken) =>
        GetAsync(Url($"/guest/groups/{Uri.EscapeDataString(groupReservationId)}/status"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> SubmitGroupPreOrderAsync(string groupReservationId, SubmitGroupPreOrderRequest request, string? bearerToken) =>
        Api.PostAsync(Url($"/group-reservations/{Uri.EscapeDataString(groupReservationId)}/submit"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> RemoveParticipantAsync(string groupReservationId, string guestSessionId, string? bearerToken) =>
        DeleteAsync(Url($"/guest/groups/{Uri.EscapeDataString(groupReservationId)}/sessions/{Uri.EscapeDataString(guestSessionId)}"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });
}
