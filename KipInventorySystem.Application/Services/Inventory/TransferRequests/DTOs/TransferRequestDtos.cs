using System.ComponentModel;
using KipInventorySystem.Domain.Enums;

namespace KipInventorySystem.Application.Services.Inventory.TransferRequests.DTOs;

public class CreateTransferRequestDraftRequest
{
    [DefaultValue("d2719a22-6ee4-47f9-8cd0-68d26a596f6f")]
    public Guid SourceWarehouseId { get; set; }

    [DefaultValue("b7336f70-ef34-445a-ad6f-1256458b6c2b")]
    public Guid DestinationWarehouseId { get; set; }

    [DefaultValue("Move stock to Abuja branch for weekend sales.")]
    public string? Notes { get; set; }

    public List<CreateTransferRequestLineRequest> Lines { get; set; } = [];
}

public class CreateTransferRequestLineRequest
{
    [DefaultValue("7a708820-8f7e-42b2-86b0-97f703f46e6a")]
    public Guid ProductId { get; set; }

    [DefaultValue(30)]
    public int QuantityRequested { get; set; }
}

public class TransferRequestLineDto
{
    public Guid TransferRequestLineId { get; set; }
    public Guid ProductId { get; set; }
    public int QuantityRequested { get; set; }
    public int QuantityTransferred { get; set; }
}

public class TransferRequestDto
{
    public Guid TransferRequestId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public Guid SourceWarehouseId { get; set; }
    public Guid DestinationWarehouseId { get; set; }
    public TransferRequestStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public List<TransferRequestLineDto> Lines { get; set; } = [];
}
