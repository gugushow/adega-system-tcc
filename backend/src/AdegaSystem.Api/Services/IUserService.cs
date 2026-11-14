using AdegaSystem.Api.Common;
using AdegaSystem.Api.DTOs.Users;

namespace AdegaSystem.Api.Services;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetAllAsync(UserQuery query);
    Task<UserDto> GetByIdAsync(int id);
    Task<UserDto> CreateAsync(CreateUserDto dto);
    Task<UserDto> UpdateAsync(int id, UpdateUserDto dto);
    Task DeactivateAsync(int id);
}
