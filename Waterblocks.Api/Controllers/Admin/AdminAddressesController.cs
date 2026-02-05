using Microsoft.AspNetCore.Mvc;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Services;

namespace Waterblocks.Api.Controllers.Admin;

[ApiController]
[Route("admin/addresses")]
public class AdminAddressesController : AdminControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly IAddressGenerator _addressGenerator;

    public AdminAddressesController(
        WorkspaceContext workspace,
        FireblocksDbContext context,
        IAddressGenerator addressGenerator)
        : base(workspace)
    {
        _context = context;
        _addressGenerator = addressGenerator;
    }

    [HttpGet("generate")]
    public ActionResult<AdminResponse<AdminAddressDto>> Generate([FromQuery] string? assetId)
    {
        if (!TryGetWorkspaceId<AdminAddressDto>(out _, out var failure))
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(assetId))
        {
            return BadRequest(AdminResponse<AdminAddressDto>.Failure("AssetId is required", "ASSET_REQUIRED"));
        }

        var address = _addressGenerator.GenerateExternalAddress(assetId);
        var asset = _context.Assets.FirstOrDefault(a => a.AssetId == assetId);
        var tag = asset?.BlockchainType == Models.BlockchainType.MemoBased
            ? _addressGenerator.GenerateMemoTag()
            : null;
        return Ok(AdminResponse<AdminAddressDto>.Success(new AdminAddressDto
        {
            Address = address,
            Tag = tag,
        }));
    }
}
