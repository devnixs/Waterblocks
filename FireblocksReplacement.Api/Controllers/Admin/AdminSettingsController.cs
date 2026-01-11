using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FireblocksReplacement.Api.Infrastructure.Db;
using FireblocksReplacement.Api.Dtos.Admin;
using FireblocksReplacement.Api.Models;

namespace FireblocksReplacement.Api.Controllers.Admin;

[ApiController]
[Route("admin/settings")]
public class AdminSettingsController : ControllerBase
{
    private readonly FireblocksDbContext _context;

    public AdminSettingsController(FireblocksDbContext context)
    {
        _context = context;
    }

    [HttpGet("auto-transitions")]
    public async Task<ActionResult<AdminResponse<AdminAutoTransitionSettingsDto>>> GetAutoTransitions()
    {
        var setting = await _context.AdminSettings.FirstOrDefaultAsync(s => s.Key == "AutoTransitionEnabled");
        var enabled = setting != null && bool.TryParse(setting.Value, out var value) && value;
        return Ok(AdminResponse<AdminAutoTransitionSettingsDto>.Success(new AdminAutoTransitionSettingsDto
        {
            Enabled = enabled
        }));
    }

    [HttpPost("auto-transitions")]
    public async Task<ActionResult<AdminResponse<AdminAutoTransitionSettingsDto>>> SetAutoTransitions(
        [FromBody] AdminAutoTransitionSettingsDto request)
    {
        var setting = await _context.AdminSettings.FirstOrDefaultAsync(s => s.Key == "AutoTransitionEnabled");
        if (setting == null)
        {
            setting = new AdminSetting { Key = "AutoTransitionEnabled" };
            _context.AdminSettings.Add(setting);
        }

        setting.Value = request.Enabled.ToString();
        setting.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(AdminResponse<AdminAutoTransitionSettingsDto>.Success(new AdminAutoTransitionSettingsDto
        {
            Enabled = request.Enabled
        }));
    }
}
