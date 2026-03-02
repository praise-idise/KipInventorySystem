using KipInventorySystem.Application.Services.Auth.DTOs;
using KipInventorySystem.Domain.Entities;
using Mapster;

namespace KipInventorySystem.Application.Services.Auth;

public class AuthMapper : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<RegisterDTO, ApplicationUser>()
            .Map(dest => dest.UserName, src => src.Email)
            .Map(dest => dest.TokenVersion, src => 0);
    }
}
