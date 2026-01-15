using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Dtos.Fireblocks;

namespace Waterblocks.Api.Controllers;

[ApiController]
public class NetworkFeeController : ControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<NetworkFeeController> _logger;

    public NetworkFeeController(FireblocksDbContext context, ILogger<NetworkFeeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("estimate_network_fee")]
    public async Task<ActionResult<NetworkFeeResponseDto>> EstimateNetworkFee([FromQuery] string assetId)
    {
        var asset = await _context.Assets.FindAsync(assetId);
        if (asset == null)
        {
            throw new KeyNotFoundException($"Asset {assetId} not found");
        }

        // Return fixed fee estimation values
        var response = new NetworkFeeResponseDto
        {
            Low = new FeeEstimateDto
            {
                FeePerByte = "10",
                GasPrice = "20000000000",
                NetworkFee = "0.00042",
                BaseFee = "15000000000",
                PriorityFee = "1000000000",
            },
            Medium = new FeeEstimateDto
            {
                FeePerByte = "20",
                GasPrice = "30000000000",
                NetworkFee = "0.00063",
                BaseFee = "15000000000",
                PriorityFee = "2000000000",
            },
            High = new FeeEstimateDto
            {
                FeePerByte = "30",
                GasPrice = "50000000000",
                NetworkFee = "0.00105",
                BaseFee = "15000000000",
                PriorityFee = "5000000000",
            },
        };

        return Ok(response);
    }
}
