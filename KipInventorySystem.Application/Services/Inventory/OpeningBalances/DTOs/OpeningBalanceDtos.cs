using System.ComponentModel;
using System.Text.Json.Serialization;

namespace KipInventorySystem.Application.Services.Inventory.OpeningBalances.DTOs;

public class CreateOpeningBalanceRequest
{
    [DefaultValue("d2719a22-6ee4-47f9-8cd0-68d26a596f6f")]
    public Guid WarehouseId { get; set; }

    [DefaultValue("Opening stock loaded during system go-live.")]
    public string? Notes { get; set; }

    public List<CreateOpeningBalanceLineRequest> Lines { get; set; } = [];
}

public class CreateOpeningBalanceLineRequest
{
    [DefaultValue("7a708820-8f7e-42b2-86b0-97f703f46e6a")]
    public Guid ProductId { get; set; }

    [DefaultValue(95)]
    public int Quantity { get; set; }

    [DefaultValue(215000.00)]
    public decimal UnitCost { get; set; }
}

public class OpeningBalanceLineDto
{
    public Guid OpeningBalanceLineId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

public class OpeningBalanceDto
{
    public Guid OpeningBalanceId { get; set; }
    public string OpeningBalanceNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public DateTime AppliedAt { get; set; }
    public string? Notes { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OpeningBalanceLineDto>? Lines { get; set; } = [];
}
