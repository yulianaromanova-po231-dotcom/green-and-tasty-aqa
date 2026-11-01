using Restaurant_Aqa_Framework.Screenplay.Tasks;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

/// <summary>The bearer token a <see cref="Requester"/> would send: the owner's or another customer's id token (the
/// other customer is signed up the first time they are needed), the waiter's id token, a guest's session token (a
/// guest joins on the spot if nobody has yet) or nothing at all for a visitor.</summary>
public sealed class TheCredentialsOf(Requester requester) : IQuestion<string?>
{
    public async Task<string?> AnsweredByAsync(Actor actor)
    {
        switch (requester)
        {
            case Requester.Owner:
                return actor.Recall<CustomerAccount>(Personas.Owner).IdToken;
            case Requester.AnotherCustomer:
                if (!actor.RecallAll<CustomerAccount>().Any(account => account.Persona == Personas.AnotherCustomer))
                    await actor.AttemptsToAsync(new SignUpAsCustomer(Personas.AnotherCustomer));
                return actor.Recall<CustomerAccount>(Personas.AnotherCustomer).IdToken;
            case Requester.Waiter:
                return actor.Recall<WaiterAccount>().IdToken;
            case Requester.Guest:
                if (actor.RecallAll<GuestAccount>().Count == 0)
                    await actor.AttemptsToAsync(new JoinTheGroup("Curious Guest"));
                return actor.RecallAll<GuestAccount>()[0].Token;
            case Requester.Visitor:
                return null;
            default:
                throw new InvalidOperationException($"Unknown requester {requester}.");
        }
    }
}
