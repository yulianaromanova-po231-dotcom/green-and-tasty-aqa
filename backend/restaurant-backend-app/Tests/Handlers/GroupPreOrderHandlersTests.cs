using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.GroupReservation;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

/// <summary>
/// HTTP contract of the owner group pre-order endpoints: status codes and JSON shape
/// exactly as documented in homework-backend.md.
/// </summary>
public class GroupPreOrderHandlersTests
{
    private const string UserId = "owner-user";

    private readonly Mock<IGuestSessionService> _guestSessions = new();
    private readonly Mock<IGroupReservationService> _groupReservations = new();
    private readonly ILambdaContext _context;

    public GroupPreOrderHandlersTests()
    {
        var context = new Mock<ILambdaContext>();
        context.Setup(c => c.Logger).Returns(new Mock<ILambdaLogger>().Object);
        _context = context.Object;
    }

    private static APIGatewayProxyRequest Request(Dictionary<string, string> path, string? body = null, bool authenticated = true) => new()
    {
        PathParameters = path,
        Body = body,
        Headers = new Dictionary<string, string>(),
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
        {
            Authorizer = authenticated
                ? new APIGatewayCustomAuthorizerContext { Claims = new Dictionary<string, string> { ["sub"] = UserId } }
                : null,
        },
    };

    private static readonly GetAllSessionsResponse Snapshot = new(
        [
            new Participant("session1", ParticipantType.Guest, "Andrii",
                [new GroupSessionItemResponse(new GroupSessionDishResponse("dish1", "Caesar Salad", 180m), 2)], 360m, true),
            new Participant("session2", ParticipantType.Guest, "Olena",
                [new GroupSessionItemResponse(new GroupSessionDishResponse("dish2", "Tom Yum", 220m), 1)], 220m, false),
        ],
        580m);

    private static Dictionary<string, string> GroupPath => new() { ["groupReservationId"] = "group-1" };

    // ---------- GET /guest/groups/{groupReservationId}/sessions ----------

    [Fact]
    public async Task GetGroupSessions_Success_Returns200WithCamelCaseContract()
    {
        _guestSessions.Setup(s => s.GetGroupSessionsAsync(UserId, "group-1")).ReturnsAsync(Snapshot);

        var response = await new GetGroupSessionsHandler(_guestSessions.Object).HandleRequest(Request(GroupPath), _context);

        Assert.Equal(200, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        var root = json.RootElement;
        Assert.Equal(580m, root.GetProperty("total").GetDecimal());
        var first = root.GetProperty("participants")[0];
        Assert.Equal("session1", first.GetProperty("id").GetString());
        Assert.Equal("Guest", first.GetProperty("type").GetString());
        Assert.Equal("Andrii", first.GetProperty("name").GetString());
        Assert.True(first.GetProperty("isSubmitted").GetBoolean());
        Assert.Equal(360m, first.GetProperty("total").GetDecimal());
        var item = first.GetProperty("items")[0];
        Assert.Equal(2, item.GetProperty("quantity").GetInt32());
        Assert.Equal("dish1", item.GetProperty("dish").GetProperty("id").GetString());
        Assert.Equal("Caesar Salad", item.GetProperty("dish").GetProperty("name").GetString());
        Assert.Equal(180m, item.GetProperty("dish").GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task GetGroupSessions_NoAuthClaims_Returns401()
    {
        var response = await new GetGroupSessionsHandler(_guestSessions.Object)
            .HandleRequest(Request(GroupPath, authenticated: false), _context);

        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task GetGroupSessions_GroupNotFound_Returns404()
    {
        _guestSessions.Setup(s => s.GetGroupSessionsAsync(UserId, "group-1")).ThrowsAsync(new GroupReservationNotFoundException());

        var response = await new GetGroupSessionsHandler(_guestSessions.Object).HandleRequest(Request(GroupPath), _context);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task GetGroupSessions_NotOwner_Returns403()
    {
        _guestSessions.Setup(s => s.GetGroupSessionsAsync(UserId, "group-1")).ThrowsAsync(new ActionForbiddenException("nope"));

        var response = await new GetGroupSessionsHandler(_guestSessions.Object).HandleRequest(Request(GroupPath), _context);

        Assert.Equal(403, response.StatusCode);
    }

    // ---------- GET /reservations/{reservationId}/guest-session-id ----------

    [Fact]
    public async Task GetOwnerGuestSessionId_Success_Returns200WithGuestSessionId()
    {
        _guestSessions.Setup(s => s.GetOwnerGuestSessionIdAsync(UserId, "reservation-1"))
            .ReturnsAsync(new OwnerGuestSessionIdResponse("owner-session-123"));

        var response = await new GetOwnerGuestSessionIdHandler(_guestSessions.Object)
            .HandleRequest(Request(new() { ["reservationId"] = "reservation-1" }), _context);

        Assert.Equal(200, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal("owner-session-123", json.RootElement.GetProperty("guestSessionId").GetString());
    }

    [Theory]
    [InlineData("reservation")]
    [InlineData("group")]
    public async Task GetOwnerGuestSessionId_NotFound_Returns404(string missing)
    {
        Exception ex = missing == "reservation"
            ? new ReservationNotFoundException("not found")
            : new GroupReservationNotFoundException();
        _guestSessions.Setup(s => s.GetOwnerGuestSessionIdAsync(UserId, "reservation-1")).ThrowsAsync(ex);

        var response = await new GetOwnerGuestSessionIdHandler(_guestSessions.Object)
            .HandleRequest(Request(new() { ["reservationId"] = "reservation-1" }), _context);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task GetOwnerGuestSessionId_NotOwner_Returns403()
    {
        _guestSessions.Setup(s => s.GetOwnerGuestSessionIdAsync(UserId, "reservation-1")).ThrowsAsync(new ActionForbiddenException("nope"));

        var response = await new GetOwnerGuestSessionIdHandler(_guestSessions.Object)
            .HandleRequest(Request(new() { ["reservationId"] = "reservation-1" }), _context);

        Assert.Equal(403, response.StatusCode);
    }

    // ---------- POST /group-reservations/{groupReservationId}/submit ----------

    [Fact]
    public async Task SubmitGroupPreOrder_Success_Returns200WithConsolidatedArray()
    {
        _groupReservations.Setup(s => s.SubmitGroupPreOrderAsync(UserId, "group-1", "reservation-123"))
            .ReturnsAsync([new ConsolidatedOrderItemResponse("dish1", 3), new ConsolidatedOrderItemResponse("dish2", 2)]);

        var response = await new SubmitGroupPreOrderHandler(_groupReservations.Object)
            .HandleRequest(Request(GroupPath, """{ "reservationId": "reservation-123" }"""), _context);

        Assert.Equal(200, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Equal("dish1", json.RootElement[0].GetProperty("dishId").GetString());
        Assert.Equal(3, json.RootElement[0].GetProperty("quantity").GetInt32());
        Assert.Equal(2, json.RootElement[1].GetProperty("quantity").GetInt32());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{ "reservationId": "" }""")]
    public async Task SubmitGroupPreOrder_InvalidBody_Returns400(string? body)
    {
        var response = await new SubmitGroupPreOrderHandler(_groupReservations.Object)
            .HandleRequest(Request(GroupPath, body), _context);

        Assert.Equal(400, response.StatusCode);
        _groupReservations.Verify(s => s.SubmitGroupPreOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupPreOrder_NoSubmittedSessions_Returns400()
    {
        _groupReservations.Setup(s => s.SubmitGroupPreOrderAsync(UserId, "group-1", "reservation-123"))
            .ThrowsAsync(new ValidationException("There are no submitted guest sessions to consolidate."));

        var response = await new SubmitGroupPreOrderHandler(_groupReservations.Object)
            .HandleRequest(Request(GroupPath, """{ "reservationId": "reservation-123" }"""), _context);

        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("reservation")]
    [InlineData("group")]
    public async Task SubmitGroupPreOrder_NotFound_Returns404(string missing)
    {
        Exception ex = missing == "reservation"
            ? new ReservationNotFoundException("not found")
            : new GroupReservationNotFoundException();
        _groupReservations.Setup(s => s.SubmitGroupPreOrderAsync(UserId, "group-1", "reservation-123")).ThrowsAsync(ex);

        var response = await new SubmitGroupPreOrderHandler(_groupReservations.Object)
            .HandleRequest(Request(GroupPath, """{ "reservationId": "reservation-123" }"""), _context);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task SubmitGroupPreOrder_NotOwner_Returns403()
    {
        _groupReservations.Setup(s => s.SubmitGroupPreOrderAsync(UserId, "group-1", "reservation-123"))
            .ThrowsAsync(new ActionForbiddenException("nope"));

        var response = await new SubmitGroupPreOrderHandler(_groupReservations.Object)
            .HandleRequest(Request(GroupPath, """{ "reservationId": "reservation-123" }"""), _context);

        Assert.Equal(403, response.StatusCode);
    }

    // ---------- DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId} ----------

    private static Dictionary<string, string> DeletePath => new() { ["groupReservationId"] = "group-1", ["guestSessionId"] = "session1" };

    [Fact]
    public async Task DeleteGuestSession_Success_Returns200WithCamelCaseSnapshot()
    {
        _guestSessions.Setup(s => s.DeleteGuestSessionAsync(UserId, "group-1", "session1")).ReturnsAsync(Snapshot);

        var response = await new DeleteGuestSessionHandler(_guestSessions.Object).HandleRequest(Request(DeletePath), _context);

        Assert.Equal(200, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        Assert.True(json.RootElement.TryGetProperty("participants", out _));
        Assert.Equal(580m, json.RootElement.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task DeleteGuestSession_SessionNotFound_Returns404()
    {
        _guestSessions.Setup(s => s.DeleteGuestSessionAsync(UserId, "group-1", "session1"))
            .ThrowsAsync(new GuestSessionNotFoundException("not found"));

        var response = await new DeleteGuestSessionHandler(_guestSessions.Object).HandleRequest(Request(DeletePath), _context);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task DeleteGuestSession_SessionFromAnotherGroup_Returns400()
    {
        _guestSessions.Setup(s => s.DeleteGuestSessionAsync(UserId, "group-1", "session1"))
            .ThrowsAsync(new ValidationException("Guest session does not belong to this group reservation."));

        var response = await new DeleteGuestSessionHandler(_guestSessions.Object).HandleRequest(Request(DeletePath), _context);

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task DeleteGuestSession_NotOwner_Returns403()
    {
        _guestSessions.Setup(s => s.DeleteGuestSessionAsync(UserId, "group-1", "session1"))
            .ThrowsAsync(new ActionForbiddenException("nope"));

        var response = await new DeleteGuestSessionHandler(_guestSessions.Object).HandleRequest(Request(DeletePath), _context);

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task DeleteGuestSession_NoAuthClaims_Returns401()
    {
        var response = await new DeleteGuestSessionHandler(_guestSessions.Object)
            .HandleRequest(Request(DeletePath, authenticated: false), _context);

        Assert.Equal(401, response.StatusCode);
    }
}
