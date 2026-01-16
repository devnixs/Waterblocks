using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public interface IAdminTransactionMapper
{
    Task<AdminTransactionDto> MapAsync(Transaction transaction, string workspaceId);
    Task<List<AdminTransactionDto>> MapAsync(IEnumerable<Transaction> transactions, string workspaceId);
}

public sealed class AdminTransactionMapper : IAdminTransactionMapper
{
    private readonly ITransactionViewService _transactionView;

    public AdminTransactionMapper(ITransactionViewService transactionView)
    {
        _transactionView = transactionView;
    }

    public async Task<AdminTransactionDto> MapAsync(Transaction transaction, string workspaceId)
    {
        var addressLookup = await _transactionView.BuildAddressOwnershipLookupAsync(new[] { transaction }, workspaceId);
        return _transactionView.MapToAdminDto(transaction, addressLookup, workspaceId);
    }

    public async Task<List<AdminTransactionDto>> MapAsync(IEnumerable<Transaction> transactions, string workspaceId)
    {
        var materialized = transactions.ToList();
        if (materialized.Count == 0)
        {
            return new List<AdminTransactionDto>();
        }

        var addressLookup = await _transactionView.BuildAddressOwnershipLookupAsync(materialized, workspaceId);
        return materialized.Select(t => _transactionView.MapToAdminDto(t, addressLookup, workspaceId)).ToList();
    }
}
