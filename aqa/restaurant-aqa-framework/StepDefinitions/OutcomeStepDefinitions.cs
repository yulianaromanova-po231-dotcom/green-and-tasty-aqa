using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.StepDefinitions;

/// <summary>Business-level outcomes shared by the features ("the owner is told …", "could not be found", "refused as
/// not the owner"). How an outcome shows up on the wire — which status code, which error body — is decided here and
/// only here, so feature files never mention it.</summary>
[Binding]
public sealed class OutcomeStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    /// <summary>Not the owner (another customer, the waiter): the backend refuses with 403. No customer sign-in at all
    /// (a visitor, a guest with only a guest session): the API gateway's Cognito authorizer turns it away with 401.</summary>
    [Then("the request is {access_outcome}")]
    public async Task ThenTheRequestIs(string outcome)
    {
        var answer = await Actor.AsksForAsync(new TheOutcome());
        var expected = outcome == "refused as not the owner" ? 403 : 401;
        await Assert.That(answer.Status).IsEqualTo(expected).Because($"response body was: {answer.Body}");
    }

    [Then("the owner is told {string}")]
    public Task ThenTheOwnerIsTold(string message) => ThenSomebodyIsTold("the owner", message);

    [Then("{string} is told {string}")]
    public async Task ThenSomebodyIsTold(string who, string message)
    {
        var answer = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(answer.Succeeded).IsFalse().Because($"{who}'s request unexpectedly succeeded: {answer.Body}");
        await Assert.That(answer.Status).IsEqualTo(400).Because($"response body was: {answer.Body}");
        await Assert.That(answer.ErrorMessage).IsEqualTo(message);
    }

    [Then("the owner is told that the {missing_thing} could not be found")]
    public async Task ThenTheOwnerIsToldNotFound(string thing)
    {
        var answer = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(answer.Succeeded).IsFalse().Because($"the request unexpectedly succeeded: {answer.Body}");

        // A missing order is a broken pre-condition of the submit (400 in the homework), the rest are missing resources (404).
        var expectedStatus = thing == "booking's pre-order" ? 400 : 404;
        await Assert.That(answer.Status).IsEqualTo(expectedStatus).Because($"response body was: {answer.Body}");
        await Assert.That(answer.ErrorMessage).Contains("not found").Because($"response body was: {answer.Body}");
    }

    [Then("the owner is told the participant is not part of this group pre-order")]
    public async Task ThenTheParticipantIsNotPartOfTheGroup()
    {
        var answer = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(answer.Status).IsEqualTo(400).Because($"response body was: {answer.Body}");
        await Assert.That(answer.ErrorMessage).IsEqualTo("Guest session does not belong to this group reservation.");
    }
}
