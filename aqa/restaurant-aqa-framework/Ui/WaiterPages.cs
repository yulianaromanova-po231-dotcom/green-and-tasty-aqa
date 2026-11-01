using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui;

/// <summary>The waiter's reservation list, <c>/waiter-reservations</c>.</summary>
public sealed class WaiterReservationsPage(IPage page) : BasePage(page)
{
    public ILocator Cards => Page.Locator("app-card-reservation");

    public Task NavigateAsync() => NavigateToAsync("/waiter-reservations");

    public ILocator CardAt(string address) => Cards.Filter(new() { HasText = address });

    public ILocator PreOrderButton(string address) =>
        CardAt(address).GetByRole(AriaRole.Button, new() { Name = "Pre-order", Exact = true });
}

/// <summary>The waiter's view of one reservation's pre-order, <c>/waiter-pre-order</c>: a card per dish with its quantity.</summary>
public sealed class WaiterPreOrderPage(IPage page) : BasePage(page)
{
    public ILocator Title => Page.Locator("h2.title");
    public ILocator DishCards => Page.Locator("app-dish-card.pre-order-dish");

    public static ILocator NameOf(ILocator card) => card.Locator(".dish-name");

    public static ILocator QuantityOf(ILocator card) => card.Locator(".pre-order-quantity");
}
