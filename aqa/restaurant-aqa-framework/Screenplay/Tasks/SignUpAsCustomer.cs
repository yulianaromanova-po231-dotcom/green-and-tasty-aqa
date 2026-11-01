using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Signs a fresh customer up and in, and remembers the account under the persona's name. The backend has no
/// way to delete an account, so every run uses a new <c>aqa.sample.&lt;guid&gt;@example.com</c> address.</summary>
public sealed class SignUpAsCustomer(string persona) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var auth = new AuthApi(actor.UsingTheApi());
        var response = actor.TheLastResponse();
        var registration = SampleDataFactory.RegisterRequest();

        await response.CaptureAsync(await auth.RegisterAsync(registration));
        if (!response.Succeeded)
            throw new InvalidOperationException($"Signing up the {persona} failed with {response.Status}: {response.Body}");

        await response.CaptureAsync(await auth.SignInAsync(new SignInRequest(registration.Email, registration.Password)));
        if (!response.Succeeded)
            throw new InvalidOperationException($"Signing in the {persona} failed with {response.Status}: {response.Body}");

        actor.Remember(persona, new CustomerAccount(persona, registration.Email, registration.Password, response.Json<SignInResponse>().IdToken));
    }
}
