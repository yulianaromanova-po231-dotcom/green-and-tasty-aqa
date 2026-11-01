using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Ui;

/// <summary>The owner's group pre-order screen, <c>/reservations/{reservationId}/group-order</c>: participant cards,
/// the consolidated pre-order, the grand total, Submit and Remove Guest with their confirmation dialogs.</summary>
public sealed class GroupOrderPage(IPage page) : BasePage(page)
{
    public ILocator Heading => Page.GetByRole(AriaRole.Heading, new() { Name = "Group Pre-Order", Level = 1 });
    public ILocator StatusBadge => Page.Locator(".title-row .badge");
    public ILocator ParticipantCards => Page.Locator("app-participant-card");
    public ILocator ConsolidatedLines => Page.Locator(".consolidated-item");
    public ILocator GrandTotal => Page.Locator(".grand-total-value");
    public ILocator SubmitButton => Page.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order", Exact = true });
    public ILocator SentButton => Page.GetByRole(AriaRole.Button, new() { Name = "Pre-Order Sent", Exact = true });
    public ILocator RefreshButton => Page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true });
    public ILocator LoadError => Page.GetByRole(AriaRole.Alert);
    public ILocator Dialog => Page.GetByRole(AriaRole.Dialog);
    public ILocator ReadyMarker => Page.Locator(".sync-text");

    public Task NavigateAsync(string reservationId) => NavigateToAsync($"/reservations/{Uri.EscapeDataString(reservationId)}/group-order");

    public ParticipantCardComponent Card(string title) =>
        new(ParticipantCards.Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = title, Exact = true }) }));

    public ParticipantCardComponent CardAt(int index) => new(ParticipantCards.Nth(index));

    public async Task SubmitAsync()
    {
        await SubmitButton.ClickAsync();
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true }).ClickAsync();
    }

    public async Task RemoveGuestAsync(string guestName)
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = $"Remove {guestName}", Exact = true }).ClickAsync();
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Remove Guest", Exact = true }).ClickAsync();
    }
}
