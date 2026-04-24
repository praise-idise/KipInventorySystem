using Asp.Versioning;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Application.Services.Inventory.OpeningBalances;
using KipInventorySystem.Application.Services.Inventory.OpeningBalances.DTOs;
using KipInventorySystem.Shared.Enums;
using KipInventorySystem.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class OpeningBalancesController(IOpeningBalanceService openingBalanceService) : BaseController
{

    /// <summary>
    /// Creates an opening balance for a specific product in a warehouse. This is typically used when initializing inventory records for the first time.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(OpeningBalanceDto), 200)]
    public async Task<IActionResult> Create([FromBody] CreateOpeningBalanceRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await openingBalanceService.CreateAsync(request, key, cancellationToken));
    }

    /// <summary>
    /// Retrieves the details of a specific opening balance by its unique identifier.
    /// </summary>
    /// <param name="openingBalanceId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("{openingBalanceId:guid}")]
    [ProducesResponseType(typeof(OpeningBalanceDto), 200)]
    public async Task<IActionResult> GetById(Guid openingBalanceId, CancellationToken cancellationToken)
     => ComputeResponse(await openingBalanceService.GetByIdAsync(openingBalanceId, cancellationToken));

    /// <summary>
    /// Retrieves a paginated list of opening balances, with optional filtering capabilities.
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<OpeningBalanceDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputePagedResponse(await openingBalanceService.GetAllAsync(parameters, cancellationToken));

    /// <summary>
    /// Searches for opening balances based on a search term. This  also supports pagination and filtering through query parameters.
    /// </summary>
    /// <param name="searchTerm"></param>
    /// <param name="parameters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<OpeningBalanceDto>), 200)]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputePagedResponse(await openingBalanceService.SearchAsync(searchTerm, parameters, cancellationToken));
}
