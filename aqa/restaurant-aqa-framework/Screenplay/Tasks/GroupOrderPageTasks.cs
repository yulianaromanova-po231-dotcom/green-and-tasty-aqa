using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Dtos.Locations;
using Restaurant_Aqa_Framework.Ui;
using static Microsoft.Playwright.Assertions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Signs a persona in through the storefront's login form: the owner, another customer or the waiter.</summary>
public sealed class SignInToTheStorefront(Requester who) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var (email, password) = who switch
        {
            Requester.Owner => Credentials(actor.Recall<CustomerAccount>(Personas.Owner)),
            Requester.AnotherCustomer => await AnotherCustomerAsync(actor),
            Requester.Waiter => (actor.Recall<WaiterAccount>().Email, actor.Recall<WaiterAccount>().Password),
            _ => throw new InvalidOperationException($"Only customers and waiters sign in, not {who}."),
        };

        await actor.AttemptsToAsync(new SignInThroughTheLoginForm(email, password));

        // The storefront only knows who signed in (and so which screens to show) once it has stored the user's profile.
        await actor.LookingAtThePage().WaitForFunctionAsync("() => localStorage.getItem('user') !== null");
    }

    private static (string, string) Credentials(CustomerAccount account) => (account.Email, account.Password);

    private static async Task<(string, string)> AnotherCustomerAsync(Actor actor)
    {
        if (!actor.RecallAll<CustomerAccount>().Any(account => account.Persona == Personas.AnotherCustomer))
            await actor.AttemptsToAsync(new SignUpAsCustomer(Personas.AnotherCustomer));
        return Credentials(actor.Recall<CustomerAccount>(Personas.AnotherCustomer));
    }
}

/// <summary>Opens the group pre-order screen of the owner's booking and waits until it has loaded (or failed to).</summary>
public sealed class OpenTheGroupPreOrderPage : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.NavigateAsync(actor.Recall<GroupInvite>(Personas.Owner).ReservationId);
        await Expect(page.ReadyMarker.Or(page.LoadError)).ToBeVisibleAsync();
    }
}

/// <summary>Asks the screen to sync now and waits for the fresh participants to arrive.</summary>
public sealed class RefreshTheGroupPreOrderPage : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var browser = actor.LookingAtThePage();
        var page = new GroupOrderPage(browser);
        await browser.RunAndWaitForResponseAsync(
            () => page.RefreshButton.ClickAsync(),
            response => response.Url.Contains("/sessions", StringComparison.Ordinal) && response.Request.Method == "GET");
        await Expect(page.RefreshButton).ToBeEnabledAsync();
    }
}

/// <summary>Submits the consolidated pre-order from the screen, confirming the dialog, and waits for the screen to
/// report it as sent.</summary>
public sealed class SubmitThroughTheGroupPreOrderPage : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.SubmitAsync();
        await Expect(page.SentButton).ToBeVisibleAsync();
    }
}

/// <summary>Removes a guest from the screen, confirming the dialog, and waits until their card is gone.</summary>
public sealed class RemoveTheGuestThroughTheGroupPreOrderPage(string guestName) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.RemoveGuestAsync(guestName);
        await Expect(page.Dialog).ToBeHiddenAsync();
        await Expect(page.Card($"Guest: {guestName}").Root).ToHaveCountAsync(0);
    }
}

/// <summary>The waiter opens their reservations, finds the owner's booking by the restaurant's address and opens its pre-order.</summary>
public sealed class OpenTheBookingsPreOrderAsTheWaiter : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var browser = actor.LookingAtThePage();
        var list = new WaiterReservationsPage(browser);
        var address = actor.Recall<LocationResponse>().Address;

        await list.NavigateAsync();
        await list.PreOrderButton(address).ClickAsync();
        await browser.WaitForURLAsync(url => url.Contains("/waiter-pre-order", StringComparison.Ordinal));
        await Expect(new WaiterPreOrderPage(browser).DishCards.First).ToBeVisibleAsync();
    }
}

/// <summary>Signs whoever is signed in out of the storefront, so another persona can sign in in the same browser.</summary>
public sealed class SignOutOfTheStorefront : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        await page.Context.ClearCookiesAsync();

        // A browser that has not opened the storefront yet (about:blank) has nobody signed in and no storage to clear.
        if (page.Url.StartsWith("http", StringComparison.Ordinal))
            await page.EvaluateAsync("() => localStorage.clear()");
    }
}
