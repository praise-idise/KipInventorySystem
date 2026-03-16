namespace KipInventorySystem.API.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresIdempotencyKeyAttribute : Attribute
{
}
