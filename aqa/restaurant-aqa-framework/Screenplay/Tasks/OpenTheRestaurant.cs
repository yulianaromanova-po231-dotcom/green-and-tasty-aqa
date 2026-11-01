using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.Locations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Dtos.Waiters;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>
/// Opens a restaurant just for this scenario: a sample location, a waiter who works there (signed up and put on the
/// location's waiter list — the backend gives every new reservation to a waiter of its location), one table big
/// enough for a group and the given dishes. Everything is prefixed <c>AQA-Sample-</c> and scheduled for removal; the
/// waiter's sign-in is remembered as the scenario's <see cref="WaiterSession"/>, so no standing waiter account is
/// needed to clean up.
/// </summary>
public sealed class OpenTheRestaurant(IReadOnlyList<(string Alias, decimal Price)> dishes, int tableSeats = 8) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var api = actor.UsingTheApi();
        var cleanup = actor.Recall<CleanupScope>();
        var response = actor.TheLastResponse();

        var locations = new LocationsApi(api);
        await response.CaptureAsync(await locations.CreateLocationAsync(SampleDataFactory.CreateLocationRequest()));
        Ensure(response, "opening the restaurant's location");
        var location = response.Json<LocationResponse>();
        actor.Remember(location);

        var waiter = await HireAWaiterAsync(actor, location.Id);
        cleanup.TrackDelete($"location:{location.Id}", () => locations.DeleteLocationAsync(location.Id, force: true, bearerToken: waiter.IdToken));

        var tables = new TablesApi(api);
        await response.CaptureAsync(await tables.CreateTableAsync(SampleDataFactory.CreateTableRequest(location.Id, tableSeats)));
        Ensure(response, "adding a table");
        actor.Remember(response.Json<TableResponse>());

        var dishesApi = new DishesApi(api);
        var menu = new Menu();
        foreach (var (alias, price) in dishes)
        {
            var request = SampleDataFactory.CreateDishRequest([location.Id], DishType.MainCourse) with
            {
                Name = $"{SampleDataFactory.Prefix}{alias}-{Guid.NewGuid().ToString("N")[..6]}",
                Price = price,
            };
            await response.CaptureAsync(await dishesApi.CreateDishAsync(request));
            Ensure(response, $"adding the dish '{alias}'");
            var dish = response.Json<DishResponse>();
            menu.Add(alias, dish);
            cleanup.TrackDelete($"dish:{dish.Id}", () => dishesApi.DeleteDishAsync(dish.Id, force: true, bearerToken: waiter.IdToken));
        }

        actor.Remember(menu);

        // Registered last, so it runs first: the waiter leaves the list before the location goes.
        var waiters = new WaitersApi(api);
        cleanup.TrackDelete($"waiter:{waiter.Email}", () => waiters.RemoveWaiterAsync(new AddWaiterRequest(waiter.Email, location.Id)));
    }

    private static async Task<WaiterAccount> HireAWaiterAsync(Actor actor, string locationId)
    {
        var api = actor.UsingTheApi();
        var response = actor.TheLastResponse();
        var auth = new AuthApi(api);
        var registration = SampleDataFactory.RegisterRequest();

        await response.CaptureAsync(await auth.RegisterAsync(registration));
        Ensure(response, "signing up the waiter");

        await response.CaptureAsync(await new WaitersApi(api).AddWaiterAsync(new AddWaiterRequest(registration.Email, locationId)));
        Ensure(response, "putting the waiter on the location's waiter list");

        // Signed in only now, so the id token carries the Waiter group.
        await response.CaptureAsync(await auth.SignInAsync(new SignInRequest(registration.Email, registration.Password)));
        Ensure(response, "signing the waiter in");

        var waiter = new WaiterAccount(registration.Email, registration.Password, response.Json<SignInResponse>().IdToken, locationId);
        actor.Remember(waiter);
        actor.Remember(new WaiterSession(waiter.IdToken));
        return waiter;
    }

    private static void Ensure(Contexts.ApiResponseContext response, string what)
    {
        if (!response.Succeeded)
            throw new InvalidOperationException($"Setting up the restaurant failed while {what}: {response.Status} {response.Body}");
    }
}
