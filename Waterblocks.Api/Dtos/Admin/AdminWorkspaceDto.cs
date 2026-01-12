namespace Waterblocks.Api.Dtos.Admin;

public class AdminWorkspaceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<AdminApiKeyDto> ApiKeys { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class AdminApiKeyDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateWorkspaceRequestDto
{
    public string Name { get; set; } = string.Empty;
}
