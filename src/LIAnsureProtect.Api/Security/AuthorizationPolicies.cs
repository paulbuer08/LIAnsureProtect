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

        options.AddPolicy(ApplicationPolicies.AcceptQuote,
            policy => policy.RequireRole(
                ApplicationRoles.Customer,
                ApplicationRoles.Broker,
                ApplicationRoles.Admin
            )
        );

        options.AddPolicy(ApplicationPolicies.UnderwriteQuote,
            policy => policy.RequireRole(
                ApplicationRoles.Underwriter,
                ApplicationRoles.Admin
            )
        );

        options.AddPolicy(ApplicationPolicies.RespondToEvidenceRequest,
            policy => policy.RequireRole(
                ApplicationRoles.Customer,
                ApplicationRoles.Broker,
                ApplicationRoles.Admin
            )
        );

        options.AddPolicy(ApplicationPolicies.BindPolicy,
            policy => policy.RequireRole(
                ApplicationRoles.Customer,
                ApplicationRoles.Broker,
                ApplicationRoles.Admin
            )
        );

        options.AddPolicy(ApplicationPolicies.ReadNotifications,
            policy => policy.RequireRole(
                ApplicationRoles.Customer,
                ApplicationRoles.Broker,
                ApplicationRoles.Underwriter,
                ApplicationRoles.Admin
            )
        );

        options.AddPolicy(ApplicationPolicies.FileClaim,
            policy => policy.RequireRole(
                ApplicationRoles.Customer,
                ApplicationRoles.Broker,
                ApplicationRoles.Admin
            )
        );

        options.AddPolicy(ApplicationPolicies.ReadClaim,
            policy => policy.RequireRole(
                ApplicationRoles.Customer,
                ApplicationRoles.Broker,
                ApplicationRoles.Admin
            )
        );

        options.AddPolicy(ApplicationPolicies.RespondToClaim,
            policy => policy.RequireRole(
                ApplicationRoles.Customer,
                ApplicationRoles.Broker,
                ApplicationRoles.Admin
            )
        );

        // The ClaimsAdjuster role — reserved since M6 — is activated here.
        options.AddPolicy(ApplicationPolicies.AdjudicateClaim,
            policy => policy.RequireRole(
                ApplicationRoles.ClaimsAdjuster,
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
