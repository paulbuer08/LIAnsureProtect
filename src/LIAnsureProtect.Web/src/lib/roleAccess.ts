export const applicationRoles = {
  customer: "Customer",
  broker: "Broker",
  underwriter: "Underwriter",
  claimsAdjuster: "ClaimsAdjuster",
  admin: "Admin",
} as const;

export const roleGroups = {
  customerWork: [
    applicationRoles.customer,
    applicationRoles.broker,
    applicationRoles.admin,
  ],
  underwritingWork: [applicationRoles.underwriter, applicationRoles.admin],
  claimsAdjudication: [
    applicationRoles.claimsAdjuster,
    applicationRoles.admin,
  ],
  notifications: [
    applicationRoles.customer,
    applicationRoles.broker,
    applicationRoles.underwriter,
    applicationRoles.claimsAdjuster,
    applicationRoles.admin,
  ],
  teamNotifications: [
    applicationRoles.underwriter,
    applicationRoles.claimsAdjuster,
    applicationRoles.admin,
  ],
} as const;

export function hasAnyRole(
  roles: readonly string[] | undefined,
  allowedRoles: readonly string[],
) {
  return Boolean(roles?.some((role) => allowedRoles.includes(role)));
}

export function hasTeamNotificationAccess(roles: readonly string[] | undefined) {
  return hasAnyRole(roles, roleGroups.teamNotifications);
}

export function getNotificationScopeLabel(roles: readonly string[] | undefined) {
  return hasTeamNotificationAccess(roles)
    ? "Personal and team notifications"
    : "Personal notifications";
}
