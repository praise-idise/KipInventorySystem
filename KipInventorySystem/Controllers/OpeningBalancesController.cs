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
public class OpeningBalancesController(IOpeningBalanceService openingBalanceService) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll([FromQuery] RequestParameters parameters, CancellationToken cancellationToken)
        => ComputeResponse(await openingBalanceService.GetAllAsync(parameters, cancellationToken));

    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm,
        [FromQuery] RequestParameters parameters,
        CancellationToken cancellationToken)
        => ComputeResponse(await openingBalanceService.SearchAsync(searchTerm, parameters, cancellationToken));

    [HttpGet("{openingBalanceId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid openingBalanceId, CancellationToken cancellationToken)
        => ComputeResponse(await openingBalanceService.GetByIdAsync(openingBalanceId, cancellationToken));

    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN, ROLE_TYPE.WAREHOUSE_OFFICER)]
    [RequiresIdempotencyKey]
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
}
