using System.Text.Json;
using BCKash.Application.Clients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BCKash.Api.Authorization;

/// <summary>
/// For endpoints under /clients/{clientId}/…: the client must be one the caller can see (else 404),
/// and — when <see cref="DocumentationOnly"/> — only whoever may document the client (the staff member
/// who onboarded them, and for an approved client only under an edit privilege) can change anything
/// (else 403). Reads stay open to anyone who can see the client. Every change a super admin makes
/// through these endpoints is recorded in the audit trail against the client.
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

        if (isRead || executed.Exception is not null || !Succeeded(executed.Result))
        {
            return;
        }

        // A saved change under an edit privilege sends an approved client back to Pending.
        if (_documentationOnly)
        {
            await _access.NoteEditedAsync(clientId, cancellationToken);
        }

        var (section, action) = Describe(context);
        var recordId = context.RouteData.Values["id"]?.ToString() ?? CreatedId(executed.Result);
        var details = JsonSerializer.Serialize(new { recordId, path = context.HttpContext.Request.Path.Value });
        await _access.RecordSuperAdminDocumentationAsync(client, section, action, details, cancellationToken);
    }

    /// <summary>"Guarantors" / "Added" for POST /clients/5/guarantors — the documentation tab and what happened in it.</summary>
    private static (string Section, string Action) Describe(ActionExecutingContext context)
    {
        var values = context.RouteData.Values;
        var section = values["controller"]?.ToString() switch
        {
            "ClientContacts" => values["kind"]?.ToString() == "references" ? "References" : "Guarantors",
            "Documents" => "Documents",
            "ClientNextOfKin" => "Next of kin",
            "ClientNextOfGuardians" => "Next of guardian",
            "ClientIdentifications" => "Identification",
            "ClientNotes" => "Notes",
            "ClientPassportPhoto" => "Passport photo",
            var other => other ?? "Documentation",
        };
        var request = context.HttpContext.Request;
        var action = request.Path.Value?.EndsWith("/photo", StringComparison.OrdinalIgnoreCase) == true
            ? "Photo uploaded"
            : request.Method.ToUpperInvariant() switch
            {
                "POST" => "Added",
                "PUT" or "PATCH" => "Updated",
                "DELETE" => "Deleted",
                var method => method,
            };
        return (section, action);
    }

    /// <summary>The new record's id when the action returned it (e.g. CreatedAtAction with an object that has an Id).</summary>
    private static string? CreatedId(IActionResult? result) =>
        (result as ObjectResult)?.Value?.GetType().GetProperty("Id")?.GetValue((result as ObjectResult)!.Value)?.ToString();

    private static bool Succeeded(IActionResult? result) => result switch
    {
        ObjectResult objectResult => (objectResult.StatusCode ?? StatusCodes.Status200OK) < 400,
        StatusCodeResult statusResult => statusResult.StatusCode < 400,
        _ => false,
    };
}
