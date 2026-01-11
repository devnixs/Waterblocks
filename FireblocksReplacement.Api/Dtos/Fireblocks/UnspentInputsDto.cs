namespace FireblocksReplacement.Api.Dtos.Fireblocks;

public class UnspentInputsResponseDto
{
    public UnspentInputDto Input { get; set; } = new();
    public string Address { get; set; } = string.Empty;
    public string Amount { get; set; } = "0";
    public decimal Confirmations { get; set; }
    public string Status { get; set; } = "CONFIRMED";
}

public class UnspentInputDto
{
    public string TxHash { get; set; } = string.Empty;
    public decimal Index { get; set; }
}
