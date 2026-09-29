using BCKash.Api.Contracts;
using BCKash.Api.Infrastructure;
using BCKash.Application.Clients;
using BCKash.Application.Communications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCKash.Api.Controllers;

/// <summary>
/// Multi-step client onboarding from the office portal, by managers and marketers only (see
/// IClientOnboardingService): check each client's BVN, then onboard a single client or a group of at
/// least three.
/// </summary>
[ApiController]
[Route("api/v1/onboarding")]
[Authorize(Policy = "Permission:clients.manage")]
public class ClientOnboardingController : ControllerBase
{
    private readonly IClientOnboardingService _onboarding;

    public ClientOnboardingController(IClientOnboardingService onboarding)
    {
        _onboarding = onboarding;
    }

    [HttpPost("bvn-check")]
    public async Task<ActionResult<BvnCheckResponse>> CheckBvn(BvnCheckRequest request, CancellationToken cancellationToken)
    {
        var result = await _onboarding.CheckBvnAsync(request.Bvn, request.FullName, request.Phone, cancellationToken);
        return result.Outcome switch
        {
            BvnCheckOutcome.Checked => Ok(new BvnCheckResponse(
                result.VerificationId!.Value,
                result.Matches,
                result.Comparisons!,
                new BvnDetailsResponse(
                    result.FromBvn!.FirstName, result.FromBvn.MiddleName, result.FromBvn.LastName,
                    result.FromBvn.Phone, result.FromBvn.BirthDate, result.FromBvn.Gender, result.FromBvn.Photo))),
            BvnCheckOutcome.NotFound => Problem(title: result.Error, statusCode: StatusCodes.Status404NotFound),
            BvnCheckOutcome.AlreadyRegistered => Problem(title: result.Error, statusCode: StatusCodes.Status409Conflict),
            BvnCheckOutcome.ProviderUnavailable => Problem(title: result.Error, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Problem(title: result.Error, statusCode: StatusCodes.Status400BadRequest),
        };
    }

    [HttpPost("clients")]
    public async Task<IActionResult> OnboardClient(OnboardSingleClientRequest request, [FromServices] IStaffNotificationService notifications, CancellationToken cancellationToken) =>
        ToResponse(await NotifyAsync(await _onboarding.OnboardClientAsync(request.OfficeId, ToInput(request.Client), cancellationToken), notifications, cancellationToken));

    [HttpPost("groups")]
    [Authorize(Policy = "Permission:groups.manage")]
    public async Task<IActionResult> OnboardGroup(OnboardGroupRequest request, [FromServices] IStaffNotificationService notifications, CancellationToken cancellationToken) =>
        ToResponse(await NotifyAsync(await _onboarding.OnboardGroupAsync(
            request.OfficeId,
            new OnboardGroupInput(request.Group?.Name ?? string.Empty, request.Group?.Phone, request.Group?.Email, request.Group?.Address),
            (request.Members ?? []).Select(ToInput).ToList(),
            cancellationToken), notifications, cancellationToken));

    /// <summary>Each newly onboarded client waits for a controller's approval.</summary>
    private static async Task<OnboardingResult> NotifyAsync(OnboardingResult result, IStaffNotificationService notifications, CancellationToken cancellationToken)
    {
        if (result.Outcome == OnboardingOutcome.Success)
        {
            foreach (var client in result.Clients!)
            {
                await notifications.ClientPendingAsync(client, afterEdit: false, cancellationToken);
            }
        }

        return result;
    }

    private static OnboardClientInput ToInput(OnboardClientRequest client) =>
        new(client.FullName, client.Email, client.Phone, client.Bvn, client.BvnVerificationId, client.DetailsSource, client.OverrideReason);

    private IActionResult ToResponse(OnboardingResult result)
    {
        switch (result.Outcome)
        {
            case OnboardingOutcome.Success:
                var body = new OnboardingResponse(
                    result.Group?.Id,
                    result.Clients!.Select(c => new OnboardedClientResponse(c.Id, c.AccountNo, c.DisplayName, c.IsHighRisk)).ToList());
                return StatusCode(StatusCodes.Status201Created, body);
            case OnboardingOutcome.Invalid:
                var problem = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: result.Errors!.Count == 1 ? result.Errors.Values.First() : "Some details need attention before onboarding.");
                problem.Extensions["errors"] = result.Errors;
                return new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
            case OnboardingOutcome.NotPermitted:
                return Problem(title: "Only managers and marketers can onboard clients.", statusCode: StatusCodes.Status403Forbidden);
            case OnboardingOutcome.OfficeOutOfScope:
                return Problem(title: "You can only onboard clients into your own office(s).", statusCode: StatusCodes.Status403Forbidden);
            default:
                return Problem(title: "Could not generate a unique account number — please retry.", statusCode: StatusCodes.Status409Conflict);
        }
    }
}
