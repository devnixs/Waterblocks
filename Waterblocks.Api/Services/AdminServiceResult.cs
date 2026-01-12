using Microsoft.AspNetCore.Mvc;
using Waterblocks.Api.Dtos.Admin;

namespace Waterblocks.Api.Services;

public sealed record AdminServiceResult<T>(AdminResponse<T> Response, int StatusCode)
{
    public ActionResult<AdminResponse<T>> ToActionResult(ControllerBase controller)
    {
        return controller.StatusCode(StatusCode, Response);
    }
}
