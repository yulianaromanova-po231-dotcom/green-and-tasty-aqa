using Restaurant_Aqa_Framework.Api.Dtos.Dishes;

namespace Restaurant_Aqa_Framework.Screenplay;

/// <summary>A customer account the scenario signed up, remembered under its persona ("owner", "another customer").</summary>
public sealed record CustomerAccount(string Persona, string Email, string Password, string IdToken);

/// <summary>The waiter of the scenario's restaurant, provisioned for this scenario only.</summary>
public sealed record WaiterAccount(string Email, string Password, string IdToken, string LocationId);

/// <summary>The invitation to a booked table's group pre-order, remembered under the owner's persona.</summary>
public sealed record GroupInvite(string ReservationId, string Token, string GroupReservationId);

/// <summary>A guest who joined a group pre-order by the invitation, remembered under the guest's name.</summary>
public sealed record GuestAccount(string Name, string SessionId, string Token, string GroupReservationId);

/// <summary>The dishes the scenario's restaurant serves, by the short name the feature file uses.</summary>
public sealed class Menu
{
    private readonly Dictionary<string, DishResponse> _byAlias = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string alias, DishResponse dish) => _byAlias[alias] = dish;

    public DishResponse this[string alias] => _byAlias.TryGetValue(alias, out var dish)
        ? dish
        : throw new InvalidOperationException($"The restaurant has no dish called '{alias}' — add it to the restaurant's dishes first.");

    /// <summary>The short name of a dish by its id or full (prefixed) name, so answers can be compared with the feature file.</summary>
    public string AliasOf(string idOrName) =>
        _byAlias.FirstOrDefault(kv => kv.Value.Id == idOrName || kv.Value.Name == idOrName).Key
        ?? throw new InvalidOperationException($"'{idOrName}' is not one of this scenario's dishes.");

    public decimal PriceOf(string alias) => this[alias].Price;
}

/// <summary>Who makes a request to the group pre-order. Only the owner of the booking may see or change it.</summary>
public enum Requester
{
    Owner,
    AnotherCustomer,
    Waiter,
    Guest,
    Visitor,
}

/// <summary>Persona names used as keys in the actor's memory.</summary>
public static class Personas
{
    public const string Owner = "owner";
    public const string AnotherCustomer = "another customer";
    public const string AnotherOwner = "another owner";
}
