namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>GET /reservations/{reservationId}/invite-token.</summary>
public sealed record InviteTokenResponse(string Token);

/// <summary>GET /guest/invites/{token}. The backend answers in PascalCase here; <see cref="JsonDefaults"/> reads it
/// case-insensitively.</summary>
public sealed record GroupInviteResponse(
    string GroupReservationId,
    string LocationAddress,
    DateTime StartTime,
    DateTime EndTime,
    int MaxGuestCount,
    string? InviterName);

/// <summary>POST /guest/sessions.</summary>
public sealed record CreateGuestSessionRequest(string GroupReservationId, string GuestName);

/// <summary>POST /guest/sessions answer (PascalCase on the wire).</summary>
public sealed record GuestSessionCreatedResponse(string SessionId, string SessionToken);

/// <summary>POST|DELETE /guest/sessions/{guestSessionId}/dishes.</summary>
public sealed record GuestDishRequest(string DishId, int Quantity);

/// <summary>GET and DELETE /guest/groups/{groupReservationId}/sessions: every participant with dishes and totals.</summary>
public sealed record GroupSessionsResponse(IReadOnlyList<ParticipantResponse> Participants, decimal Total);

public sealed record ParticipantResponse(
    string? Id,
    string Type,
    string Name,
    IReadOnlyList<ParticipantItemResponse> Items,
    decimal Total,
    bool IsSubmitted);

public sealed record ParticipantItemResponse(ParticipantDishResponse Dish, int Quantity);

public sealed record ParticipantDishResponse(string Id, string Name, decimal Price);

/// <summary>GET /guest/groups/{groupReservationId}/status.</summary>
public sealed record GroupStatusResponse(int CurrentParticipants, int MaxCapacity, string Status);

/// <summary>GET /reservations/{reservationId}/guest-session-id.</summary>
public sealed record OwnerSessionIdResponse(string GuestSessionId);

/// <summary>POST /group-reservations/{groupReservationId}/submit.</summary>
public sealed record SubmitGroupPreOrderRequest(string ReservationId);

/// <summary>One line of the consolidated pre-order the submit returns.</summary>
public sealed record ConsolidatedItemResponse(string DishId, int Quantity);
