using System.Net.Http.Headers;
using LoadIx.Application.Documents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace LoadIx.Api.Endpoints;

public static class DocumentEndpoints
{
    public static RouteGroupBuilder MapDocumentEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/documents")
            .WithTags("Documents")
            .RequireAuthorization();
        group.MapGet("/", ListAsync);
        group.MapPost("/", UploadAsync)
            .RequireRateLimiting("document-upload")
            .DisableAntiforgery();
        group.MapGet("/{id:guid}/content", (
            Guid id,
            HttpContext context,
            IDocumentService service,
            CancellationToken cancellationToken) =>
            DownloadAsync(id, false, context, service, cancellationToken));
        group.MapGet("/{id:guid}/preview", (
            Guid id,
            HttpContext context,
            IDocumentService service,
            CancellationToken cancellationToken) =>
            DownloadAsync(id, true, context, service, cancellationToken));
        group.MapDelete("/{id:guid}", DeleteAsync);
        return api;
    }

    private static async Task<IResult> ListAsync(
        [FromQuery] string ownerType,
        [FromQuery] Guid ownerId,
        IDocumentService service,
        CancellationToken cancellationToken) =>
        ToResult(await service.ListAsync(
            ownerType,
            ownerId,
            cancellationToken));

    private static async Task<IResult> UploadAsync(
        HttpContext context,
        IDocumentService service,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasFormContentType)
        {
            return Problem(
                DocumentFailure.Validation,
                "document.form_required",
                "A multipart form upload is required.");
        }

        var form = await context.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return Problem(
                DocumentFailure.Validation,
                "document.file_required",
                "A document file is required.");
        }

        if (!Guid.TryParse(form["ownerId"], out var ownerId))
        {
            return Problem(
                DocumentFailure.Validation,
                "document.owner_invalid",
                "A valid document owner is required.");
        }

        await using var stream = file.OpenReadStream();
        var result = await service.UploadAsync(
            new DocumentUploadModel(
                form["ownerType"].ToString(),
                ownerId,
                form["documentType"].ToString(),
                EmptyToNull(form["notes"]),
                file.FileName,
                file.ContentType,
                file.Length),
            stream,
            Header(context, "Idempotency-Key"),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToResult(result);
        }

        context.Response.Headers.ETag = $"\"{result.Value!.Version}\"";
        return Results.Created(
            $"/api/v1/documents/{result.Value.Id}",
            result.Value);
    }

    private static async Task<IResult> DownloadAsync(
        Guid id,
        bool preview,
        HttpContext context,
        IDocumentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DownloadAsync(id, preview, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToResult(result);
        }

        var value = result.Value!;
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'none'; sandbox";
        context.Response.Headers["Digest"] =
            $"sha-256=:{Convert.ToBase64String(Convert.FromHexString(value.Sha256Checksum))}:";
        if (preview)
        {
            context.Response.Headers.ContentDisposition =
                ContentDispositionHeaderValue("inline", value.FileName);
            return Results.Stream(
                value.Content,
                value.ContentType,
                enableRangeProcessing: false);
        }

        context.Response.Headers.ContentDisposition =
            ContentDispositionHeaderValue("attachment", value.FileName);
        return Results.Stream(
            value.Content,
            "application/octet-stream",
            enableRangeProcessing: false);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        HttpContext context,
        IDocumentService service,
        CancellationToken cancellationToken)
    {
        var version = Header(context, "If-Match").Trim('"');
        var result = await service.DeleteAsync(id, version, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : ToResult(result);
    }

    private static IResult ToResult<T>(DocumentResult<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : Problem(result.Failure, result.Code, result.Message);

    private static IResult Problem(
        DocumentFailure failure,
        string code,
        string message)
    {
        var status = failure switch
        {
            DocumentFailure.Validation => StatusCodes.Status400BadRequest,
            DocumentFailure.Forbidden => StatusCodes.Status403Forbidden,
            DocumentFailure.NotFound => StatusCodes.Status404NotFound,
            DocumentFailure.Conflict => StatusCodes.Status409Conflict,
            DocumentFailure.StorageUnavailable =>
                StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };
        return Results.Problem(
            statusCode: status,
            title: failure.ToString(),
            detail: message,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
            });
    }

    private static string Header(HttpContext context, string name)
    {
        context.Request.Headers.TryGetValue(name, out StringValues value);
        return value.ToString();
    }

    private static string? EmptyToNull(StringValues value)
    {
        var text = value.ToString().Trim();
        return text.Length == 0 ? null : text;
    }

    private static string ContentDispositionHeaderValue(
        string disposition,
        string fileName)
    {
        var header = new ContentDispositionHeaderValue(disposition)
        {
            FileNameStar = fileName,
        };
        return header.ToString();
    }
}
