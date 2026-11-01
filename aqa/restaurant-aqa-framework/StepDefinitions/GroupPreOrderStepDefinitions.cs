using System.Globalization;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;

namespace Restaurant_Aqa_Framework.StepDefinitions;

/// <summary>
/// The group pre-order: the owner books, guests join and fill their carts, the owner reviews, submits and removes
/// participants. Set-up always goes through the API; the same When/Then sentences run through the API in
/// <c>@api</c> scenarios and through the owner's and waiter's screens in <c>@ui</c> scenarios.
/// </summary>
[Binding]
public sealed class GroupPreOrderStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    // ---------------------------------------------------------------- set-up

    [Given("a restaurant serves these dishes:")]
    public Task GivenARestaurantServes(DataTable dishes) =>
        Actor.AttemptsToAsync(new OpenTheRestaurant(dishes.Rows
            .Select(row => (row["dish"], decimal.Parse(row["price"], CultureInfo.InvariantCulture)))
            .ToList()));

    [Given("the owner has booked a table for {int} guests")]
    public Task GivenTheOwnerHasBooked(int guests) => Actor.AttemptsToAsync(new BookAGroupTable(Personas.Owner, guests));

    [Given("another owner has booked the same table later the same day")]
    public Task GivenAnotherOwnerHasBooked() => Actor.AttemptsToAsync(new BookAGroupTable(Personas.AnotherOwner, 4, hourOffset: 2));

    [Given("the owner has pre-ordered {string}")]
    public Task GivenTheOwnerHasPreOrdered(string dishes) => Actor.AttemptsToAsync(new PlaceOwnPreOrder(DishList.Parse(dishes)));

    [Given("these guests have joined the group pre-order:")]
    public async Task GivenTheseGuestsHaveJoined(DataTable guests)
    {
        foreach (var row in guests.Rows)
        {
            var name = row["guest"];
            await Actor.AttemptsToAsync(new JoinTheGroup(name));
            foreach (var (dish, quantity) in DishList.Parse(row["dishes"]))
                await Actor.AttemptsToAsync(new AddToTheCart(name, dish, quantity));
            if (Yes(row["cart confirmed"]))
                await Actor.AttemptsToAsync(new ConfirmTheCart(name));
        }
    }

    [Given("{string} has joined the group pre-order")]
    public Task GivenGuestHasJoined(string guest) => Actor.AttemptsToAsync(new JoinTheGroup(guest));

    [Given("{string} has joined the other owner's group pre-order")]
    public Task GivenGuestHasJoinedTheOtherGroup(string guest) => Actor.AttemptsToAsync(new JoinTheGroup(guest, Personas.AnotherOwner));

    [Given("the owner has submitted the group pre-order")]
    public async Task GivenTheOwnerHasSubmitted()
    {
        await Actor.AttemptsToAsync(new SubmitTheGroupPreOrder());
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        if (!outcome.Succeeded)
            throw new InvalidOperationException($"Submitting the group pre-order failed: {outcome.Status} {outcome.Body}");
    }

    // ---------------------------------------------------------------- guests

    [When("{string} adds {int} {string} to the cart")]
    public Task WhenGuestAdds(string guest, int quantity, string dish) => Actor.AttemptsToAsync(new AddToTheCart(guest, dish, quantity));

    [When("{string} tries to confirm the cart")]
    public Task WhenGuestTriesToConfirm(string guest) => Actor.AttemptsToAsync(new ConfirmTheCart(guest, mustSucceed: false));

    // ---------------------------------------------------------------- reviewing

    [Given("the owner reviews the group pre-order")]
    [When("the owner reviews the group pre-order")]
    [Scope(Tag = "api")]
    public Task WhenTheOwnerReviewsThroughTheApi() => Actor.AttemptsToAsync(new ReviewTheGroupPreOrder());

    [Given("the owner reviews the group pre-order")]
    [When("the owner reviews the group pre-order")]
    [Scope(Tag = "ui")]
    public Task WhenTheOwnerReviewsOnScreen() =>
        Actor.AttemptsToAsync(new SignInToTheStorefront(Requester.Owner), new OpenTheGroupPreOrderPage());

    [When("the owner reviews a group pre-order that does not exist")]
    public Task WhenTheOwnerReviewsAMissingGroup() => Actor.AttemptsToAsync(new ReviewTheGroupPreOrder(ofAMissingGroup: true));

    [When("{requester} asks to see the group pre-order")]
    public Task WhenSomebodyAsksToSee(Requester requester) => Actor.AttemptsToAsync(new ReviewTheGroupPreOrder(requester));

    [When("the owner refreshes the group pre-order screen")]
    public Task WhenTheOwnerRefreshes() => Actor.AttemptsToAsync(new RefreshTheGroupPreOrderPage());

    [When("another customer opens the owner's group pre-order screen")]
    public Task WhenAnotherCustomerOpensTheScreen() =>
        Actor.AttemptsToAsync(new SignInToTheStorefront(Requester.AnotherCustomer), new OpenTheGroupPreOrderPage());

    [Then("the owner sees these participants:")]
    [Scope(Tag = "api")]
    public async Task ThenTheOwnerSeesThroughTheApi(DataTable expected)
    {
        var shown = await Actor.AsksForAsync(new TheGroupPreOrderShown());
        var actual = shown.Participants.Where(p => p.Type == "Guest")
            .Select(p => Row(p.Name, p.Dishes, p.Total, p.CartConfirmed))
            .OrderBy(row => row, StringComparer.Ordinal).ToList();

        await Assert.That(actual).IsEquivalentTo(ExpectedRows(expected)).Because($"the owner was shown: {shown.Body}");
    }

    [Then("the owner sees these participants:")]
    [Scope(Tag = "ui")]
    public async Task ThenTheOwnerSeesOnScreen(DataTable expected)
    {
        var page = await Actor.AsksForAsync(new TheGroupOrderPage());
        var actual = page.Cards.Where(card => card.Title.StartsWith("Guest: ", StringComparison.Ordinal))
            .Select(card => Row(card.Title["Guest: ".Length..], card.Dishes, card.Subtotal, card.Status == "Submitted"))
            .OrderBy(row => row, StringComparer.Ordinal).ToList();

        await Assert.That(actual).IsEquivalentTo(ExpectedRows(expected)).Because($"the screen showed: {page.Text}");
    }

    [Then("the grand total is {int}")]
    public async Task ThenTheGrandTotalIs(int total)
    {
        var shown = await Actor.AsksForAsync(new TheGroupPreOrderShown());
        await Assert.That(shown.GrandTotal).IsEqualTo(total).Because($"the owner was shown: {shown.Body}");
    }

    [Then("the grand total equals the sum of the participants' totals")]
    public async Task ThenTheGrandTotalIsTheSum()
    {
        var shown = await Actor.AsksForAsync(new TheGroupPreOrderShown());
        await Assert.That(shown.GrandTotal).IsEqualTo(shown.Participants.Sum(p => p.Total)).Because($"the owner was shown: {shown.Body}");
    }

    [Then("the owner sees that nobody has joined yet")]
    public async Task ThenNobodyHasJoined()
    {
        var shown = await Actor.AsksForAsync(new TheGroupPreOrderShown());
        await Assert.That(shown.Participants).IsEmpty().Because($"the owner was shown: {shown.Body}");
        await Assert.That(shown.GrandTotal).IsEqualTo(0m).Because($"the owner was shown: {shown.Body}");
    }

    [Then("the screen consolidates only the confirmed carts:")]
    public async Task ThenTheScreenConsolidates(DataTable expected)
    {
        var page = await Actor.AsksForAsync(new TheGroupOrderPage());
        await Assert.That(Lines(page.Consolidated)).IsEquivalentTo(ExpectedLines(expected)).Because($"the screen showed: {page.Text}");
    }

    [Then("the screen shows a grand total of {int}")]
    public async Task ThenTheScreenShowsAGrandTotal(int total)
    {
        var page = await Actor.AsksForAsync(new TheGroupOrderPage());
        await Assert.That(page.GrandTotal).IsEqualTo(total).Because($"the screen showed: {page.Text}");
    }

    [Then("the screen says that nobody has added a dish yet")]
    public async Task ThenTheScreenSaysNoDishes()
    {
        var page = await Actor.AsksForAsync(new TheGroupOrderPage());
        await Assert.That(page.Text).Contains("Participants haven’t added any dishes yet").Because($"the screen showed: {page.Text}");
    }

    [Then("the owner cannot submit the group pre-order")]
    public async Task ThenTheOwnerCannotSubmit()
    {
        var page = await Actor.AsksForAsync(new TheGroupOrderPage());
        await Assert.That(page.CanSubmit).IsFalse().Because($"the screen showed: {page.Text}");
    }

    [Then("the screen says the group pre-order could not be loaded")]
    public async Task ThenTheScreenCouldNotLoad()
    {
        var error = await Actor.AsksForAsync(new TheGroupOrderPageError());
        await Assert.That(error).IsNotNull().Because("the screen loaded somebody else's group pre-order");
        await Assert.That(error!).Contains("Couldn’t load the group pre-order");
    }

    // ---------------------------------------------------------------- submitting

    [When("the owner submits the group pre-order")]
    [Scope(Tag = "api")]
    public Task WhenTheOwnerSubmitsThroughTheApi() => Actor.AttemptsToAsync(new SubmitTheGroupPreOrder());

    [When("the owner submits the group pre-order")]
    [Scope(Tag = "ui")]
    public Task WhenTheOwnerSubmitsOnScreen() => Actor.AttemptsToAsync(new SubmitThroughTheGroupPreOrderPage());

    [When("{requester} tries to submit the group pre-order")]
    public Task WhenSomebodyTriesToSubmit(Requester requester) => Actor.AttemptsToAsync(new SubmitTheGroupPreOrder(requester));

    [When("the owner submits the group pre-order for a booking that does not exist")]
    public Task WhenTheOwnerSubmitsForAMissingBooking() => Actor.AttemptsToAsync(new SubmitTheGroupPreOrder(target: SubmitTarget.AMissingBooking));

    [When("the owner submits a group pre-order that does not exist")]
    public Task WhenTheOwnerSubmitsAMissingGroup() => Actor.AttemptsToAsync(new SubmitTheGroupPreOrder(target: SubmitTarget.AMissingGroup));

    [Then("the restaurant receives this consolidated pre-order:")]
    public async Task ThenTheRestaurantReceives(DataTable expected)
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Succeeded).IsTrue().Because($"the submit was refused: {outcome.Status} {outcome.Body}");
        var consolidated = await Actor.AsksForAsync(new TheConsolidatedPreOrder());
        await Assert.That(Lines(consolidated)).IsEquivalentTo(ExpectedLines(expected)).Because($"the restaurant received: {outcome.Body}");
    }

    [Then("the booking's pre-order contains:")]
    public async Task ThenTheBookingsPreOrderContains(DataTable expected)
    {
        var order = await Actor.AsksForAsync(new TheReservationOrder(Requester.Owner));
        await Assert.That(Lines(order.Dishes)).IsEquivalentTo(ExpectedLines(expected)).Because($"the booking's pre-order is: {order.Body}");
    }

    [Then("the booking's pre-order is marked as submitted by the customer")]
    public async Task ThenTheOrderIsSubmittedByTheCustomer()
    {
        var order = await Actor.AsksForAsync(new TheReservationOrder(Requester.Owner));
        await Assert.That(order.Status).IsEqualTo(OrderStatus.SubmittedByCustomer).Because($"the booking's pre-order is: {order.Body}");
    }

    [Then("the group pre-order is marked as submitted")]
    public async Task ThenTheGroupIsSubmitted() =>
        await Assert.That(await Actor.AsksForAsync(new TheGroupStatus())).IsEqualTo("Submitted");

    [Then("the group pre-order is still open")]
    public async Task ThenTheGroupIsStillOpen() =>
        await Assert.That(await Actor.AsksForAsync(new TheGroupStatus())).IsEqualTo("Open");

    [Then("the waiter sees this pre-order for the booking:")]
    [Scope(Tag = "api")]
    public async Task ThenTheWaiterSeesThroughTheApi(DataTable expected)
    {
        var order = await Actor.AsksForAsync(new TheReservationOrder(Requester.Waiter));
        await Assert.That(Lines(order.Dishes)).IsEquivalentTo(ExpectedLines(expected)).Because($"the waiter sees: {order.Body}");
        await Assert.That(order.Status).IsEqualTo(OrderStatus.SubmittedByCustomer);
    }

    [Then("the waiter sees this pre-order for the booking:")]
    [Scope(Tag = "ui")]
    public async Task ThenTheWaiterSeesOnScreen(DataTable expected)
    {
        await Actor.AttemptsToAsync(new SignOutOfTheStorefront(), new SignInToTheStorefront(Requester.Waiter), new OpenTheBookingsPreOrderAsTheWaiter());
        var shown = await Actor.AsksForAsync(new TheWaiterPreOrderShown());
        await Assert.That(Lines(shown)).IsEquivalentTo(ExpectedLines(expected));
    }

    [Then("the screen shows the pre-order as sent to the restaurant")]
    public async Task ThenTheScreenShowsSent()
    {
        var page = await Actor.AsksForAsync(new TheGroupOrderPage());
        await Assert.That(page.Badge).IsEqualTo("Sent to Restaurant").Because($"the screen showed: {page.Text}");
        await Assert.That(page.ShownAsSent).IsTrue().Because($"the screen showed: {page.Text}");
        await Assert.That(page.Cards.Any(card => card.CanBeRemoved)).IsFalse().Because("guests can no longer be removed once the pre-order is sent");
    }

    // ---------------------------------------------------------------- removing

    [When("the owner removes {string} from the group pre-order")]
    [Scope(Tag = "api")]
    public Task WhenTheOwnerRemovesThroughTheApi(string guest) => Actor.AttemptsToAsync(new RemoveTheParticipant(RemovalTarget.Guest, guest));

    [When("the owner removes {string} from the group pre-order")]
    [Scope(Tag = "ui")]
    public Task WhenTheOwnerRemovesOnScreen(string guest) => Actor.AttemptsToAsync(new RemoveTheGuestThroughTheGroupPreOrderPage(guest));

    [When("the owner removes {string} who belongs to the other group")]
    public Task WhenTheOwnerRemovesAGuestOfAnotherGroup(string guest) =>
        Actor.AttemptsToAsync(new RemoveTheParticipant(RemovalTarget.GuestOfAnotherGroup, guest));

    [When("the owner removes a participant who never joined")]
    public Task WhenTheOwnerRemovesNobody() => Actor.AttemptsToAsync(new RemoveTheParticipant(RemovalTarget.Nobody));

    [When("the owner tries to remove their own place in the group")]
    public Task WhenTheOwnerRemovesThemselves() => Actor.AttemptsToAsync(new RemoveTheParticipant(RemovalTarget.TheOwnersOwnSession));

    [When("{requester} tries to remove {string}")]
    public Task WhenSomebodyTriesToRemove(Requester requester, string guest) =>
        Actor.AttemptsToAsync(new RemoveTheParticipant(RemovalTarget.Guest, guest, requester));

    // ---------------------------------------------------------------- helpers

    private static bool Yes(string value) => value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);

    private static string Row(string name, string dishes, decimal total, bool confirmed) =>
        $"{name} | {dishes} | {total.ToString("0.##", CultureInfo.InvariantCulture)} | {(confirmed ? "yes" : "no")}";

    private static List<string> ExpectedRows(DataTable table) => table.Rows
        .Select(row => Row(row["participant"], DishList.Normalize(row["dishes"]), decimal.Parse(row["total"], CultureInfo.InvariantCulture), Yes(row["cart confirmed"])))
        .OrderBy(row => row, StringComparer.Ordinal).ToList();

    private static List<string> Lines(IReadOnlyDictionary<string, int> dishes) =>
        dishes.Select(kv => $"{kv.Value} x {kv.Key}").OrderBy(line => line, StringComparer.Ordinal).ToList();

    private static List<string> ExpectedLines(DataTable table) => table.Rows
        .Select(row => $"{row["quantity"]} x {row["dish"]}").OrderBy(line => line, StringComparer.Ordinal).ToList();
}
