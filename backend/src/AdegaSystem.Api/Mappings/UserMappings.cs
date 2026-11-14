using AdegaSystem.Api.DTOs.Users;
using AdegaSystem.Api.Entities;

namespace AdegaSystem.Api.Mappings;

public static class UserMappings
{
    public static UserDto ToDto(this User user) => new(user.Id, user.Name, user.Email, user.Role, user.Active, user.CreatedAt);
}
