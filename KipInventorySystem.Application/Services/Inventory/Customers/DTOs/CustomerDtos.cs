using System.ComponentModel;

namespace KipInventorySystem.Application.Services.Inventory.Customers.DTOs;

public class CreateCustomerRequest
{
    [DefaultValue("Walk-in Customer")]
    public string Name { get; set; } = string.Empty;

    [DefaultValue("customer@example.com")]
    public string? Email { get; set; }

    [DefaultValue("+2348012345678")]
    public string? Phone { get; set; }
}

public class UpdateCustomerRequest
{
    [DefaultValue("Walk-in Customer Updated")]
    public string? Name { get; set; }

    [DefaultValue("updated.customer@example.com")]
    public string? Email { get; set; }

    [DefaultValue("+2348098765432")]
    public string? Phone { get; set; }
}

public class CustomerDto
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
