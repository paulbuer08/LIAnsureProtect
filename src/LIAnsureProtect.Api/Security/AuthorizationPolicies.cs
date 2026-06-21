using LIAnsureProtect.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace LIAnsureProtect.Api.Security;


public static class AuthorizationPolicies
{
    public static void AddApplicationAuthorizationPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(ApplicationPolicies.CreateSubmission, 
            policy => policy.RequireRole(
                ApplicationRoles.Customer,
                ApplicationRoles.Broker,
                ApplicationRoles.Admin
            )
        );

        options.AddPolicy(ApplicationPolicies.ReadSubmission,
            policy => policy.RequireRole(
                ApplicationRoles.Customer,
                ApplicationRoles.Broker,
                ApplicationRoles.Admin
            )
        );

        options.AddPolicy(ApplicationPolicies.SubmitSubmission,
            policy => policy.RequireRole(
                ApplicationRoles.Customer,
                ApplicationRoles.Broker,
                ApplicationRoles.Admin
            )
        );

        options.AddPolicy(ApplicationPolicies.CreateQuote,
            policy => policy.RequireRole(
                ApplicationRoles.Customer,
                ApplicationRoles.Broker,
                ApplicationRoles.Admin
            )
        );

        options.AddPolicy(ApplicationPolicies.AdminAccess,
            policy => policy.RequireRole(
                ApplicationRoles.Admin
            )
        );
    }

}
