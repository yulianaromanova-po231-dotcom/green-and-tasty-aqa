using System.Globalization;
using Restaurant_Aqa_Framework.Ui;
using static Microsoft.Playwright.Assertions;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

/// <summary>One participant card as the screen shows it.</summary>
public sealed record ParticipantCardView(string Title, string Status, string Dishes, decimal Subtotal, bool CanBeRemoved);

/// <summary>Everything the owner can read on the group pre-order screen.</summary>
public sealed record GroupOrderPageView(
    string Badge,
    IReadOnlyList<ParticipantCardView> Cards,
    IReadOnlyDictionary<string, int> Consolidated,
    decimal GrandTotal,
    bool CanSubmit,
    bool ShownAsSent,
    string Text);

/// <summary>Reads the group pre-order screen once it is ready (UI questions do their own waiting).</summary>
public sealed class TheGroupOrderPage : IQuestion<GroupOrderPageView>
{
    public async Task<GroupOrderPageView> AnsweredByAsync(Actor actor)
    {
        var browser = actor.LookingAtThePage();
        var page = new GroupOrderPage(browser);
        var menu = actor.Recall<Menu>();
        await Expect(page.ReadyMarker).ToBeVisibleAsync();

        var cards = new List<ParticipantCardView>();
        for (var i = 0; i < await page.ParticipantCards.CountAsync(); i++)
        {
            var card = page.CardAt(i);
            var names = await card.DishNames.AllInnerTextsAsync();
            var quantities = await card.DishQuantities.AllInnerTextsAsync();
            var dishes = names.Zip(quantities, (name, quantity) => (menu.AliasOf(name.Trim()), Quantity(quantity)));
            cards.Add(new ParticipantCardView(
                (await card.Title.InnerTextAsync()).Trim(),
                (await card.Status.InnerTextAsync()).Replace("✓", "", StringComparison.Ordinal).Replace("…", "", StringComparison.Ordinal).Trim(),
                DishList.Describe(dishes),
                Money(await card.Subtotal.InnerTextAsync()),
                await card.RemoveButton.CountAsync() > 0));
        }

        var consolidated = new Dictionary<string, int>();
        foreach (var line in await page.ConsolidatedLines.Locator("span:first-child").AllInnerTextsAsync())
        {
            var pieces = line.Split(" × ", 2, StringSplitOptions.TrimEntries);
            consolidated[menu.AliasOf(pieces[0])] = int.Parse(pieces[1], CultureInfo.InvariantCulture);
        }

        return new GroupOrderPageView(
            (await page.StatusBadge.InnerTextAsync()).Trim(),
            cards,
            consolidated,
            Money(await page.GrandTotal.InnerTextAsync()),
            await page.SubmitButton.CountAsync() > 0 && await page.SubmitButton.IsEnabledAsync(),
            await page.SentButton.CountAsync() > 0,
            await browser.Locator("main").InnerTextAsync());
    }

    internal static decimal Money(string text) =>
        decimal.Parse(text.Replace("$", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal).Trim(), CultureInfo.InvariantCulture);

    private static int Quantity(string text) =>
        int.Parse(new string(text.Where(char.IsDigit).ToArray()), CultureInfo.InvariantCulture);
}

/// <summary>Whether the screen could not load the group pre-order at all, and what it says.</summary>
public sealed class TheGroupOrderPageError : IQuestion<string?>
{
    public async Task<string?> AnsweredByAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await Expect(page.ReadyMarker.Or(page.LoadError)).ToBeVisibleAsync();
        return await page.LoadError.CountAsync() > 0 ? (await page.LoadError.InnerTextAsync()).Trim() : null;
    }
}

/// <summary>The dishes and quantities on the waiter's pre-order screen.</summary>
public sealed class TheWaiterPreOrderShown : IQuestion<IReadOnlyDictionary<string, int>>
{
    public async Task<IReadOnlyDictionary<string, int>> AnsweredByAsync(Actor actor)
    {
        var page = new WaiterPreOrderPage(actor.LookingAtThePage());
        var menu = actor.Recall<Menu>();
        await Expect(page.DishCards.First).ToBeVisibleAsync();

        var answer = new Dictionary<string, int>();
        for (var i = 0; i < await page.DishCards.CountAsync(); i++)
        {
            var card = page.DishCards.Nth(i);
            var name = (await WaiterPreOrderPage.NameOf(card).InnerTextAsync()).Trim();
            var quantity = await WaiterPreOrderPage.QuantityOf(card).InnerTextAsync();
            answer[menu.AliasOf(name)] = int.Parse(new string(quantity.Where(char.IsDigit).ToArray()), CultureInfo.InvariantCulture);
        }

        return answer;
    }
}
