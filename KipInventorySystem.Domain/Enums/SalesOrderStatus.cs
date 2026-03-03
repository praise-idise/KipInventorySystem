namespace KipInventorySystem.Domain.Enums;

public enum SalesOrderStatus : int
{
    Draft = 1,
    Confirmed,
    PartiallyFulfilled,
    Fulfilled,
    Cancelled
}
