using System.Threading.Tasks;

namespace KipInventorySystem.Infrastructure.Seeder;

public interface IApplicationSeeder
{
    Task SeedAsync();
}
