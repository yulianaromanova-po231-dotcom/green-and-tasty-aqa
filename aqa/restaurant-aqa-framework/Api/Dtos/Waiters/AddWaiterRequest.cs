namespace Restaurant_Aqa_Framework.Api.Dtos.Waiters;

/// <summary>POST /waiters (and DELETE /waiters): puts a registered user on a location's waiter list.</summary>
public sealed record AddWaiterRequest(string Email, string LocationId);
