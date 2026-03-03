using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Domain.Entities;

public class TransferRequestLine : BaseEntity
{
    [Key]
    public Guid TransferRequestLineId { get; set; } = Guid.CreateVersion7();

    public Guid TransferRequestId { get; set; }
    public TransferRequest TransferRequest { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public int QuantityRequested { get; set; }
    public int QuantityTransferred { get; set; }
}
