using Microsoft.AspNetCore.Authorization;

namespace Asms.Api.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
