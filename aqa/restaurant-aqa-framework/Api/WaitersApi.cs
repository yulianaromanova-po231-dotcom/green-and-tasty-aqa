using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Dtos.Waiters;

namespace Restaurant_Aqa_Framework.Api;

/// <summary>The waiter list of a location. Signing up always yields a customer; adding the account here makes it a
/// waiter of that location (Cognito group "Waiter"), which is how the suite provisions a waiter per scenario.</summary>
public sealed class WaitersApi(IAPIRequestContext api) : ApiClientBase(api)
{
    public Task<IAPIResponse> AddWaiterAsync(AddWaiterRequest request) =>
        Api.PostAsync(Url("/waiters"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(),
        });

    public Task<IAPIResponse> RemoveWaiterAsync(AddWaiterRequest request) =>
        DeleteAsync(Url("/waiters"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(),
        });
}
