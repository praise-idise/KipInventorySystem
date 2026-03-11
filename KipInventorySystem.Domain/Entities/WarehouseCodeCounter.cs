using System.ComponentModel.DataAnnotations;

namespace KipInventorySystem.Domain.Entities;

public class WarehouseCodeCounter
{
    [Key]
    [MaxLength(3)]
    public string StateCode { get; set; } = string.Empty;

    public int LastNumber { get; set; }
}
