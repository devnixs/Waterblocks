using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FireblocksReplacement.Api.Infrastructure.Db;
using FireblocksReplacement.Api.Dtos.Fireblocks;

namespace FireblocksReplacement.Api.Controllers;

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

        var baseFee = GetBaseFee(assetId);

        var response = new NetworkFeeResponseDto
        {
            AssetId = assetId,
            Low = new FeeEstimateDto { Fee = (baseFee * 0.8m).ToString("F8"), GasPrice = "20" },
            Medium = new FeeEstimateDto { Fee = baseFee.ToString("F8"), GasPrice = "30" },
            High = new FeeEstimateDto { Fee = (baseFee * 1.5m).ToString("F8"), GasPrice = "50" }
        };

        return Ok(response);
    }

    private decimal GetBaseFee(string assetId)
    {
        return assetId switch
        {
            "BTC" => 0.0001m,
            "ETH" => 0.001m,
            "USDT" => 0.0005m,
            "USDC" => 0.0005m,
            _ => 0.0001m
        };
    }
}
