using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.Locations;
using Moq;

namespace APIHandler.Tests.Services;

/// <summary>
/// Level 2: POST /group-reservations/{groupReservationId}/submit —
/// <see cref="GroupReservationService.SubmitGroupPreOrderAsync"/>.
/// </summary>
public class GroupPreOrderSubmitTests
{
    private const string OwnerId = "owner-user";
    private const string GroupId = "group-1";
    private const string ReservationId = "reservation-1";

    private readonly Mock<IGroupReservationRepository> _groups = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IGuestSessionRepository> _sessions = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly GroupReservationService _service;

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

    private readonly Order _order = new()
    {
        OrderId = "order-1",
        CustomerId = OwnerId,
        ReservationId = ReservationId,
        ReservationAddress = "48 Rustaveli Avenue, Table 1",
        ReservationTime = "Oct 17, 2026, 12:00 - 14:00",
        Dishes = [],
        Status = OrderStatus.Created,
    };

    public GroupPreOrderSubmitTests()
    {
        _service = new GroupReservationService(
            _groups.Object,
            new Mock<ITableRepository>().Object,
            _reservations.Object,
            _sessions.Object,
            new Mock<IUserService>().Object,
            new Mock<ILocationService>().Object,
            _orders.Object);

        _groups.Setup(r => r.GetGroupReservationByIdAsync(GroupId)).ReturnsAsync(_group);
        _reservations.Setup(r => r.GetReservationByIdAsync(ReservationId)).ReturnsAsync(_reservation);
        _orders.Setup(r => r.GetOrderByReservationIdAsync(ReservationId)).ReturnsAsync(_order);
    }

    private static GuestSession Session(string id, bool submitted, params (string DishId, int Qty)[] dishes) => new()
    {
        Id = id,
        GroupReservationId = GroupId,
        GuestName = id,
        IsSubmitted = submitted,
        Dishes = dishes.Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Qty }).ToList(),
    };

    private void SetupSessions(params GuestSession[] sessions) =>
        _sessions.Setup(r => r.GetAllByGroupReservationIdAsync(GroupId)).ReturnsAsync(sessions.ToList());

    private void VerifyNothingSaved()
    {
        _orders.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
        _groups.Verify(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task Submit_MergesOnlySubmittedSessionsByDishId_AndUpdatesStatuses()
    {
        SetupSessions(
            Session("andrii", true, ("dish1", 2), ("dish2", 1)),
            Session("olena", true, ("dish1", 1)),
            Session("not-ready", false, ("dish3", 5)));

        var result = await _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId);

        Assert.Equal(2, result.Count);
        Assert.Equal(3, result.Single(i => i.DishId == "dish1").Quantity);
        Assert.Equal(1, result.Single(i => i.DishId == "dish2").Quantity);
        Assert.DoesNotContain(result, i => i.DishId == "dish3");

        Assert.Equal(OrderStatus.SubmittedByCustomer, _order.Status);
        Assert.Equal(3, _order.Dishes.Single(d => d.DishId == "dish1").Quantity);
        Assert.All(_order.Dishes, d => Assert.Equal(OrderItemState.Pending, d.State));
        _orders.Verify(r => r.SaveOrderAsync(_order), Times.Once);

        Assert.Equal(GroupReservationStatus.Submitted, _group.Status);
        _groups.Verify(r => r.SaveGroupReservationAsync(_group), Times.Once);
    }

    [Fact]
    public async Task Submit_DishAlreadyInOrder_QuantityIsAddedNotReplaced()
    {
        _order.Dishes.Add(new OrderItem { DishId = "dish1", Quantity = 1, State = OrderItemState.Pending });
        SetupSessions(Session("andrii", true, ("dish1", 2)));

        await _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId);

        Assert.Equal(3, Assert.Single(_order.Dishes).Quantity);
    }

    [Fact]
    public async Task Submit_DishLineWasCancelled_IsReactivatedWithGuestQuantity()
    {
        _order.Dishes.Add(new OrderItem { DishId = "dish1", Quantity = 4, State = OrderItemState.Canceled });
        SetupSessions(Session("andrii", true, ("dish1", 2)));

        await _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId);

        var line = Assert.Single(_order.Dishes);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(OrderItemState.Pending, line.State);
    }

    [Fact]
    public async Task Submit_GroupSaveFails_OrderIsRestoredSoRetryCannotDoubleQuantities()
    {
        _order.Dishes.Add(new OrderItem { DishId = "dish1", Quantity = 1, State = OrderItemState.Pending });
        SetupSessions(Session("andrii", true, ("dish1", 2), ("dish2", 1)));
        _groups.Setup(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()))
            .ThrowsAsync(new InvalidOperationException("DynamoDB throttled"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId));

        Assert.Equal(OrderStatus.Created, _order.Status);
        var line = Assert.Single(_order.Dishes);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(GroupReservationStatus.Open, _group.Status);
        _orders.Verify(r => r.SaveOrderAsync(_order), Times.Exactly(2)); // update + compensation
    }

    [Fact]
    public async Task Submit_GroupNotFound_ThrowsGroupReservationNotFound()
    {
        _groups.Setup(r => r.GetGroupReservationByIdAsync("missing")).ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, "missing", ReservationId));
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Submit_ReservationNotFound_ThrowsReservationNotFound()
    {
        _reservations.Setup(r => r.GetReservationByIdAsync("missing")).ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, "missing"));
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Submit_ReservationBelongsToAnotherGroup_ThrowsValidation()
    {
        var other = new Reservation { Id = "reservation-2", TableId = "t", WaiterId = "w", CustomerId = OwnerId };
        _reservations.Setup(r => r.GetReservationByIdAsync("reservation-2")).ReturnsAsync(other);

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, "reservation-2"));
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Submit_CallerIsNotOwner_ThrowsActionForbidden()
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.SubmitGroupPreOrderAsync("someone-else", GroupId, ReservationId));
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Submit_NoSubmittedSessions_ThrowsValidation()
    {
        SetupSessions(Session("andrii", false, ("dish1", 2)));

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId));
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Submit_SubmittedSessionsWithoutDishes_ThrowsValidation()
    {
        SetupSessions(Session("andrii", true), Session("olena", true, ("dish1", 0)));

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId));
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Submit_OrderNotFound_ThrowsValidation()
    {
        SetupSessions(Session("andrii", true, ("dish1", 1)));
        _orders.Setup(r => r.GetOrderByReservationIdAsync(ReservationId)).ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId));
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Submit_OrderCancelled_IsTreatedAsNotFound()
    {
        SetupSessions(Session("andrii", true, ("dish1", 1)));
        _order.Status = OrderStatus.Cancelled;

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId));
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Submit_GroupAlreadySubmitted_ThrowsGroupReservationSubmitted()
    {
        _group.Status = GroupReservationStatus.Submitted;

        await Assert.ThrowsAsync<GroupReservationSubmittedException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId));
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Submit_GroupClosed_ThrowsGroupReservationExpired()
    {
        _group.Status = GroupReservationStatus.Closed;

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _service.SubmitGroupPreOrderAsync(OwnerId, GroupId, ReservationId));
        VerifyNothingSaved();
    }
}
