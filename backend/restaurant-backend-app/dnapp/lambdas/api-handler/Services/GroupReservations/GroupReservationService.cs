using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Models.GuestReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.Locations;
using APIHandler.Constants.Orders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Services.GroupReservations;

public class GroupReservationService(
    IGroupReservationRepository groupReservationRepository,
    ITableRepository tableRepository,
    IReservationRepository reservationRepository,
    IGuestSessionRepository guestSessionRepository,
    IUserService userService,
    ILocationService locationService,
    IOrderRepository orderRepository) : IGroupReservationService
{
    /// <summary>
    /// Level 2: the owner confirms the consolidated group pre-order.
    /// Takes only submitted guest sessions, merges identical dishes by dishId,
    /// adds them to the reservation's order and moves the order to
    /// SubmittedByCustomer and the group to Submitted.
    /// </summary>
    public async Task<IReadOnlyList<ConsolidatedOrderItemResponse>> SubmitGroupPreOrderAsync(
        string userId, string groupReservationId, string reservationId)
    {
        var group = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();

        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException($"Reservation {reservationId} was not found.");

        if (group.ReservationId != reservation.Id)
            throw new ValidationException("The reservation does not belong to this group reservation.");

        if (reservation.CustomerId != userId)
            throw new ActionForbiddenException("Only the reservation owner can submit the group pre-order.");

        if (group.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();

        if (group.Status == GroupReservationStatus.Closed)
            throw new GroupReservationExpiredException();

        var sessions = await guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservationId) ?? [];
        var submittedSessions = sessions.Where(s => s.IsSubmitted).ToArray();

        if (submittedSessions.Length == 0)
            throw new ValidationException("There are no submitted guest sessions to consolidate.");

        var consolidated = submittedSessions
            .SelectMany(s => s.Dishes ?? [])
            .Where(d => d.Quantity > 0 && !string.IsNullOrWhiteSpace(d.DishId))
            .GroupBy(d => d.DishId)
            .Select(g => new ConsolidatedOrderItemResponse(g.Key, g.Sum(d => d.Quantity)))
            .ToArray();

        if (consolidated.Length == 0)
            throw new ValidationException("Submitted guest sessions do not contain any dishes.");

        var order = await orderRepository.GetOrderByReservationIdAsync(reservationId);
        if (order is null || order.Status == OrderStatus.Cancelled)
            throw new ValidationException($"Active order for reservation '{reservationId}' was not found.");

        // Snapshot of the order so it can be restored if the second write fails.
        order.Dishes ??= [];
        var originalStatus = order.Status;
        var originalDishes = order.Dishes
            .Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Quantity, State = d.State })
            .ToList();

        foreach (var item in consolidated)
        {
            var existing = order.Dishes.FirstOrDefault(d => d.DishId == item.DishId);
            if (existing is null)
            {
                order.Dishes.Add(new OrderItem
                {
                    DishId = item.DishId,
                    Quantity = item.Quantity,
                    State = OrderItemState.Pending,
                });
            }
            else if (existing.State == OrderItemState.Canceled)
            {
                // A cancelled line must not silently swallow the guests' dishes.
                existing.Quantity = item.Quantity;
                existing.State = OrderItemState.Pending;
            }
            else
            {
                existing.Quantity += item.Quantity;
            }
        }

        order.Status = OrderStatus.SubmittedByCustomer;
        await orderRepository.SaveOrderAsync(order);

        group.Status = GroupReservationStatus.Submitted;
        try
        {
            await groupReservationRepository.SaveGroupReservationAsync(group);
        }
        catch
        {
            // Compensate: without this, a retry would pass the "already submitted" guard
            // and add the same dishes to the order a second time.
            group.Status = GroupReservationStatus.Open;
            order.Dishes = originalDishes;
            order.Status = originalStatus;
            await orderRepository.SaveOrderAsync(order);
            throw;
        }

        return consolidated;
    }

    private const string DefaultRestaurantName = "Green & Tasty";

    public async Task<GetGroupReservationResponse> GetGroupReservationByTokenAsync(string token)
    {
        GroupReservation groupReservation = await groupReservationRepository.GetGroupReservationByTokenAsync(token)
            ?? throw new GroupReservationNotFoundException();

        if (groupReservation.Status != GroupReservationStatus.Open)
            throw new GroupReservationExpiredException();

        Task<Reservation?> reservationTask = reservationRepository.GetReservationByIdAsync(groupReservation.ReservationId);
        Task<int> countTask = guestSessionRepository.CountByGroupReservationId(groupReservation.GroupReservationId);

        Reservation reservation = await reservationTask ?? throw new ValidationException("Associated reservation not found.");
        int joinedGuestsCount = await countTask;

        if (joinedGuestsCount + 1 >= reservation.GuestCount)
            throw new GuestLimitExceededException();

        Table table = await tableRepository.GetTableAsync(reservation.TableId)
            ?? throw new InvalidOperationException("Associated table not found.");

        Location? location = await locationService.GetLocationByIdAsync(table.LocationId);
        string restaurantName = DefaultRestaurantName;
        string address = location?.Address ?? "Unknown Address";

        string inviterName = "Unknown";
        if (!string.IsNullOrWhiteSpace(reservation.CustomerId))
        {
            inviterName = await userService.GetUserName(reservation.CustomerId) ?? "Unknown";
        }
        return new GetGroupReservationResponse(
            groupReservation.GroupReservationId,
            table.LocationId,
            restaurantName,
            address,
            reservation.StartTime,
            reservation.EndTime,
            reservation.GuestCount,
            table.TableNumber,
            inviterName);
    }

    public async Task<GroupReservation> GetGroupReservationAsync(string groupReservationId)
    {
        GroupReservation? groupReservation = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId);
        return groupReservation ?? throw new InvalidOperationException($"Group reservation with ID {groupReservationId} not found.");
    }

    public async Task<GroupReservation> CreateGroupReservationAsync(string reservationId)
    {
        var groupReservation = new GroupReservation
        {
            GroupReservationId = Guid.NewGuid().ToString(),
            ReservationId = reservationId,
            ReservationToken = Guid.NewGuid().ToString("N"),
            Status = GroupReservationStatus.Open,
        };

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
        return groupReservation;
    }

    public async Task CreateGroupReservationIfDoesNotExistAsync(string userId, string reservationId)
    {
        GroupReservation? group = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        if (group is not null)
            return;

        await CreateGroupReservationAsync(reservationId);
    }

    public async Task UpdateGroupReservationStatusAsync(string groupReservationId, GroupReservationStatus status)
    {
        var groupReservation = await GetGroupReservationAsync(groupReservationId);

        groupReservation.Status = status;

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
    }

    public async Task DeleteGroupReservationAsync(string groupReservationId)
    {
        var groupReservation = await GetGroupReservationAsync(groupReservationId);

        await groupReservationRepository.DeleteGroupReservationAsync(groupReservation);
    }

    public async Task CloseGroupReservationAsync(string reservationId)
    {
        GroupReservation? groupReservation = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        if (groupReservation is null)
            return;

        groupReservation.Status = GroupReservationStatus.Closed;

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
    }

    public async Task<GetGroupInviteTokenResponse> GetGroupReservationInviteTokenAsync(string reservationId)
    {
        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException($"Reservation {reservationId} was not found.");

        if (reservation.GuestCount < 2)
            throw new ValidationException("Retrieving invitation token for the reservation with only one guest is not permitted.");

        if (reservation.Status != ReservationStatus.Reserved)
            throw new GroupReservationExpiredException("The group reservation is expired.");

        GroupReservation groupReservation = (await groupReservationRepository.GetByReservationIdAsync(reservationId))!;

        if (groupReservation.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();

        if (groupReservation.Status == GroupReservationStatus.Closed)
            throw new GroupReservationExpiredException();

        return new GetGroupInviteTokenResponse(groupReservation.ReservationToken);
    }
}

