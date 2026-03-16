using Asp.Versioning;
using KipInventorySystem.API.Attributes;
using KipInventorySystem.Application.Services.Inventory.StockIssues;
using KipInventorySystem.Application.Services.Inventory.StockIssues.DTOs;
using KipInventorySystem.Shared.Enums;
using Microsoft.AspNetCore.Mvc;

namespace KipInventorySystem.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class StockIssuesController(IStockIssueService stockIssueService) : BaseController
{
    [HttpPost]
    [Roles(ROLE_TYPE.ADMIN)]
    [RequiresIdempotencyKey]
    public async Task<IActionResult> Create([FromBody] CreateStockIssueRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateModelState();
        if (validation != null) return validation;

        if (!TryGetIdempotencyKey(out var key, out var error))
        {
            return error!;
        }

        return ComputeResponse(await stockIssueService.IssueAsync(request, key, cancellationToken));
    }
}
