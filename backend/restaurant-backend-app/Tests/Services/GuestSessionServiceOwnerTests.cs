using Amazon.CognitoIdentityProvider.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using Moq;

namespace APIHandler.Tests.Services;

/// <summary>
/// Owner-side group pre-order features of <see cref="GuestSessionService"/>:
/// level 1 (GET sessions snapshot), level 2 (owner session id) and level 3 (DELETE session).
/// </summary>
public class GuestSessionServiceOwnerTests
{
    private const string OwnerId = "owner-user";
    private const string GroupId = "group-1";
    private const string ReservationId = "reservation-1";

    private readonly Mock<IGuestSessionRepository> _sessions = new();
    private readonly Mock<IGroupReservationRepository> _groups = new();
    private readonly Mock<IDishRepository> _dishes = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITableRepository> _tables = new();
    private readonly GuestSessionService _service;

    private readonly GroupReservation _group = new()
    {
        GroupReservationId = GroupId,
        ReservationId = ReservationId,
        ReservationToken = "token",
        Status = GroupReservationStatus.Open,
    };

    private readonly Reservation _reservation = new()
    {
        Id = ReservationId,
        TableId = "table-1",
        WaiterId = "waiter-1",
        CustomerId = OwnerId,
        GuestCount = 4,
        Status = ReservationStatus.Reserved,
    };

    private static readonly Dish Caesar = new() { Id = "dish1", Name = "Caesar Salad", Price = 180 };
    private static readonly Dish TomYum = new() { Id = "dish2", Name = "Tom Yum", Price = 220 };

    public GuestSessionServiceOwnerTests()
    {
        _service = new GuestSessionService(
            _groups.Object, _sessions.Object, _dishes.Object, _reservations.Object, _users.Object, _tables.Object);

        _groups.Setup(r => r.GetGroupReservationByIdAsync(GroupId)).ReturnsAsync(_group);
        _groups.Setup(r => r.GetByReservationIdAsync(ReservationId)).ReturnsAsync(_group);
        _reservations.Setup(r => r.GetReservationByIdAsync(ReservationId)).ReturnsAsync(_reservation);
        _dishes.Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync((IEnumerable<string> ids) => new[] { Caesar, TomYum }.Where(d => ids.Contains(d.Id)).ToList());
    }

    private static GuestSession Session(string id, string name, bool submitted, params (string DishId, int Qty)[] dishes) => new()
    {
        Id = id,
        GroupReservationId = GroupId,
        GuestName = name,
        IsSubmitted = submitted,
        Dishes = dishes.Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Qty }).ToList(),
    };

    private static GuestSession OwnerSession(params (string DishId, int Qty)[] dishes)
    {
        var s = Session("owner-session", "Owner", false, dishes);
        s.IsOwnerSession = true;
        s.OwnerUserId = OwnerId;
        return s;
    }

    private void SetupSessions(params GuestSession[] sessions) =>
        _sessions.Setup(r => r.GetAllByGroupReservationIdAsync(GroupId)).ReturnsAsync(sessions.ToList());

    // ---------- Level 1: GET /guest/groups/{groupReservationId}/sessions ----------

    [Fact]
    public async Task GetGroupSessionsAsync_ReturnsParticipantsWithPerParticipantAndGrandTotals()
    {
        SetupSessions(
            Session("session1", "Andrii", true, ("dish1", 2)),
            Session("session2", "Olena", false, ("dish2", 1)));

        var result = await _service.GetGroupSessionsAsync(OwnerId, GroupId);

        var participants = result.Participants.ToList();
        Assert.Equal(2, participants.Count);

        var andrii = participants.Single(p => p.Id == "session1");
        Assert.Equal(ParticipantType.Guest, andrii.Type);
        Assert.Equal("Andrii", andrii.Name);
        Assert.True(andrii.IsSubmitted);
        Assert.Equal(360m, andrii.Total);
        var item = Assert.Single(andrii.Items);
        Assert.Equal("dish1", item.Dish.Id);
        Assert.Equal("Caesar Salad", item.Dish.Name);
        Assert.Equal(180m, item.Dish.Price);
        Assert.Equal(2, item.Quantity);

        var olena = participants.Single(p => p.Id == "session2");
        Assert.False(olena.IsSubmitted);
        Assert.Equal(220m, olena.Total);

        Assert.Equal(580m, result.Total);
    }

    [Fact]
    public async Task GetGroupSessionsAsync_OwnerSessionIsListedFirstWithOwnerTypeAndCognitoName()
    {
        SetupSessions(Session("session1", "Andrii", true, ("dish1", 1)), OwnerSession(("dish2", 2)));
        _users.Setup(r => r.GetUserByIdAsync(OwnerId)).ReturnsAsync(new UserType
        {
            Attributes = [new AttributeType { Name = "given_name", Value = "Yuliana" }],
        });

        var result = await _service.GetGroupSessionsAsync(OwnerId, GroupId);

        var first = result.Participants.First();
        Assert.Equal(ParticipantType.Owner, first.Type);
        Assert.Equal("Yuliana", first.Name);
        Assert.Equal(440m, first.Total);
        Assert.Equal(620m, result.Total);
    }

    [Fact]
    public async Task GetGroupSessionsAsync_OwnerNameLookupFails_FallsBackToSessionName()
    {
        SetupSessions(OwnerSession());
        _users.Setup(r => r.GetUserByIdAsync(OwnerId)).ThrowsAsync(new Exception("Cognito is down"));

        var result = await _service.GetGroupSessionsAsync(OwnerId, GroupId);

        Assert.Equal("Owner", Assert.Single(result.Participants).Name);
    }

    [Fact]
    public async Task GetGroupSessionsAsync_DishRemovedFromMenu_IsSkippedInsteadOfFailing()
    {
        SetupSessions(Session("session1", "Andrii", true, ("dish1", 1), ("deleted-dish", 3)));

        var result = await _service.GetGroupSessionsAsync(OwnerId, GroupId);

        var participant = Assert.Single(result.Participants);
        Assert.Single(participant.Items);
        Assert.Equal(180m, result.Total);
    }

    [Fact]
    public async Task GetGroupSessionsAsync_NoSessions_ReturnsEmptySnapshotWithoutQueryingDishes()
    {
        SetupSessions();

        var result = await _service.GetGroupSessionsAsync(OwnerId, GroupId);

        Assert.Empty(result.Participants);
        Assert.Equal(0m, result.Total);
        _dishes.Verify(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task GetGroupSessionsAsync_GroupNotFound_ThrowsGroupReservationNotFound()
    {
        _groups.Setup(r => r.GetGroupReservationByIdAsync("missing")).ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetGroupSessionsAsync(OwnerId, "missing"));
    }

    [Fact]
    public async Task GetGroupSessionsAsync_CallerIsNotOwner_ThrowsActionForbidden()
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.GetGroupSessionsAsync("someone-else", GroupId));
    }

    // ---------- Level 2: GET /reservations/{reservationId}/guest-session-id ----------

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_OwnerSessionExists_ReturnsItsId()
    {
        SetupSessions(Session("session1", "Andrii", false), OwnerSession());

        var result = await _service.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId);

        Assert.Equal("owner-session", result.GuestSessionId);
        _sessions.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_NoOwnerSessionAndGroupOpen_CreatesOwnerSession()
    {
        SetupSessions(Session("session1", "Andrii", false));
        GuestSession? saved = null;
        _sessions.Setup(r => r.SaveAsync(It.IsAny<GuestSession>())).Callback<GuestSession>(s => saved = s);

        var result = await _service.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId);

        Assert.NotNull(saved);
        Assert.Equal("owner-group-1", saved.Id); // deterministic: concurrent first calls cannot create two
        Assert.True(saved.IsOwnerSession);
        Assert.Equal(OwnerId, saved.OwnerUserId);
        Assert.Equal(GroupId, saved.GroupReservationId);
        Assert.Equal(saved.Id, result.GuestSessionId);
    }

    [Fact]
    public async Task GroupStatus_OwnerSessionIsNotCountedAsAnExtraGuest()
    {
        // Owner session exists (created by guest-session-id) + one joined guest = 2 people, not 3.
        SetupSessions(OwnerSession(), Session("session1", "Andrii", false));

        var status = await _service.GetGroupReservationStatusAsync(GroupId);

        Assert.Equal(2, status.CurrentParticipants);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_NoOwnerSessionAndGroupSubmitted_ThrowsGuestSessionNotFound()
    {
        _group.Status = GroupReservationStatus.Submitted;
        SetupSessions();

        await Assert.ThrowsAsync<GuestSessionNotFoundException>(() => _service.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId));
        _sessions.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_ReservationNotFound_ThrowsReservationNotFound()
    {
        _reservations.Setup(r => r.GetReservationByIdAsync("missing")).ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.GetOwnerGuestSessionIdAsync(OwnerId, "missing"));
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_GroupNotFound_ThrowsGroupReservationNotFound()
    {
        _groups.Setup(r => r.GetByReservationIdAsync(ReservationId)).ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId));
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_CallerIsNotOwner_ThrowsActionForbidden()
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.GetOwnerGuestSessionIdAsync("someone-else", ReservationId));
    }

    // ---------- Level 3: DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId} ----------

    [Fact]
    public async Task DeleteGuestSessionAsync_DeletesSessionAndReturnsUpdatedSnapshot()
    {
        var andrii = Session("session1", "Andrii", true, ("dish1", 2));
        var olena = Session("session2", "Olena", false, ("dish2", 1));
        _sessions.Setup(r => r.GetByGuestSessionId("session1")).ReturnsAsync(andrii);
        SetupSessions(olena); // state after deletion

        var result = await _service.DeleteGuestSessionAsync(OwnerId, GroupId, "session1");

        _sessions.Verify(r => r.DeleteAsync(andrii), Times.Once);
        var remaining = Assert.Single(result.Participants);
        Assert.Equal("session2", remaining.Id);
        Assert.Equal(220m, result.Total);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_SessionNotFound_ThrowsGuestSessionNotFound()
    {
        _sessions.Setup(r => r.GetByGuestSessionId("missing")).ReturnsAsync((GuestSession?)null);

        await Assert.ThrowsAsync<GuestSessionNotFoundException>(() => _service.DeleteGuestSessionAsync(OwnerId, GroupId, "missing"));
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_SessionFromAnotherGroup_ThrowsValidationAndDoesNotDelete()
    {
        var foreign = Session("session9", "Stranger", false);
        foreign.GroupReservationId = "other-group";
        _sessions.Setup(r => r.GetByGuestSessionId("session9")).ReturnsAsync(foreign);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.DeleteGuestSessionAsync(OwnerId, GroupId, "session9"));

        Assert.Equal(typeof(ValidationException), ex.GetType()); // plain 400, not a "not found" subtype
        _sessions.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_OwnerOwnSession_ThrowsValidationAndDoesNotDelete()
    {
        var owner = OwnerSession();
        _sessions.Setup(r => r.GetByGuestSessionId(owner.Id)).ReturnsAsync(owner);

        await Assert.ThrowsAsync<ValidationException>(() => _service.DeleteGuestSessionAsync(OwnerId, GroupId, owner.Id));
        _sessions.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_GroupAlreadySubmitted_ThrowsGroupReservationSubmitted()
    {
        _group.Status = GroupReservationStatus.Submitted;
        _sessions.Setup(r => r.GetByGuestSessionId("session1")).ReturnsAsync(Session("session1", "Andrii", true));

        await Assert.ThrowsAsync<GroupReservationSubmittedException>(() => _service.DeleteGuestSessionAsync(OwnerId, GroupId, "session1"));
        _sessions.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_CallerIsNotOwner_ThrowsActionForbiddenAndDoesNotDelete()
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.DeleteGuestSessionAsync("someone-else", GroupId, "session1"));
        _sessions.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }
}
