using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GroupReservations;
using System;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

/// <summary>
/// POST /group-reservations/{groupReservationId}/submit — owner confirms the consolidated group pre-order.
/// </summary>
public class SubmitGroupPreOrderHandler(IGroupReservationService groupReservationService) : IApiRequestHandler
{
    private readonly IGroupReservationService _groupReservationService =
        groupReservationService ?? throw new ArgumentNullException(nameof(groupReservationService));

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("groupReservationId", out var groupReservationId))
            return ResponseBuilder.CreateErrorResponse(400, "Missing 'groupReservationId' in path parameters.", context.Logger);

        var userId = UserContextHelper.GetAuthenticatedUserId(request);
        if (userId is null)
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);

        if (!RequestBodyHelper.TryParseAndValidate(request, context, out SubmitGroupPreOrderRequest? body, out APIGatewayProxyResponse? errorResponse))
            return errorResponse;

        try
        {
            var consolidated = await _groupReservationService.SubmitGroupPreOrderAsync(userId, groupReservationId, body.ReservationId);
            return ResponseBuilder.CreateJsonResponse(200, consolidated);
        }
        catch (GroupReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (ReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (ActionForbiddenException ex)
        {
            return ResponseBuilder.CreateErrorResponse(403, ex.Message, context.Logger);
        }
        catch (ValidationException ex)
        {
            return ResponseBuilder.CreateErrorCodeResponse(ex);
        }
    }
}
