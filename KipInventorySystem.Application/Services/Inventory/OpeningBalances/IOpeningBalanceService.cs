using KipInventorySystem.Application.Services.Inventory.OpeningBalances.DTOs;
using KipInventorySystem.Shared.Models;
using KipInventorySystem.Shared.Responses;

namespace KipInventorySystem.Application.Services.Inventory.OpeningBalances;

public interface IOpeningBalanceService
{
    Task<ServiceResponse<OpeningBalanceDto>> CreateAsync(
        CreateOpeningBalanceRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<OpeningBalanceDto>> GetByIdAsync(Guid openingBalanceId, CancellationToken cancellationToken = default);

    Task<ServiceResponse<PaginationResult<OpeningBalanceDto>>> GetAllAsync(
        RequestParameters parameters,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<PaginationResult<OpeningBalanceDto>>> SearchAsync(
        string? searchTerm,
        RequestParameters parameters,
        CancellationToken cancellationToken = default);
}
