using BCKash.Application.Clients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BCKash.Api.Authorization;

/// <summary>
/// For endpoints under /clients/{clientId}/…: the client must be one the caller can see (else 404),
/// and — when <see cref="DocumentationOnly"/> — only whoever may document the client (the staff member
/// who onboarded them, and for an approved client only under an edit privilege) can change anything
/// (else 403). Reads stay open to anyone who can see the client.
/// </summary>
public class ClientRecordAccessAttribute : TypeFilterAttribute
{
    public ClientRecordAccessAttribute(bool documentationOnly = true)
        : base(typeof(ClientRecordAccessFilter))
    {
        Arguments = [documentationOnly];
    }

    public bool DocumentationOnly => (bool)Arguments![0];
}

public class ClientRecordAccessFilter : IAsyncActionFilter
{
    private readonly IClientAccess _access;
    private readonly bool _documentationOnly;

    public ClientRecordAccessFilter(IClientAccess access, bool documentationOnly)
    {
        _access = access;
        _documentationOnly = documentationOnly;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!int.TryParse(context.RouteData.Values["clientId"]?.ToString(), out var clientId))
        {
            await next();
            return;
        }

        var cancellationToken = context.HttpContext.RequestAborted;
        var client = await _access.FindVisibleAsync(clientId, cancellationToken);
        if (client is null)
        {
            context.Result = new NotFoundResult();
            return;
        }

        var isRead = HttpMethods.IsGet(context.HttpContext.Request.Method);
        if (_documentationOnly && !isRead && await _access.DocumentationBlockAsync(client, cancellationToken) is { } block)
        {
            context.Result = new ObjectResult(new ProblemDetails { Title = block, Status = StatusCodes.Status403Forbidden })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        var executed = await next();

        // A saved change under an edit privilege sends an approved client back to Pending.
        if (_documentationOnly && !isRead && executed.Exception is null && Succeeded(executed.Result))
        {
            await _access.NoteEditedAsync(clientId, cancellationToken);
        }
    }

    private static bool Succeeded(IActionResult? result) => result switch
    {
        ObjectResult objectResult => (objectResult.StatusCode ?? StatusCodes.Status200OK) < 400,
        StatusCodeResult statusResult => statusResult.StatusCode < 400,
        _ => false,
    };
}
