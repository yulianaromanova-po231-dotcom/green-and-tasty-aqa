using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui.Components;

/// <summary>One <c>app-participant-card</c> on the group pre-order screen: title ("My Pre-Order", "Guest: Andrii"),
/// cart status chip, dish lines and subtotal.</summary>
public sealed class ParticipantCardComponent(ILocator root)
{
    public ILocator Root => root;
    public ILocator Title => root.GetByRole(AriaRole.Heading);
    public ILocator Status => root.Locator(".chip");
    public ILocator DishNames => root.Locator(".item .name");
    public ILocator DishQuantities => root.Locator(".item .qty");
    public ILocator Subtotal => root.Locator(".subtotal span").Last;
    public ILocator RemoveButton => root.Locator(".remove-button");
}
