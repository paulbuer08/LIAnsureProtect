export type NotificationScope = "personal" | "team";

export interface NotificationInboxItem {
  notificationId: string;
  scope: NotificationScope;
  audience: string;
  type: string;
  title: string;
  subjectReferenceType: string;
  subjectReferenceId: string;
  attributes: Record<string, string>;
  occurredAtUtc: string;
  isRead: boolean;
  readAtUtc: string | null;
  lifecycleState?: "Active" | "Historical";
  historicalAtUtc?: string | null;
  historicalReason?: string | null;
  replacementQuoteId?: string | null;
  replacementQuoteVersion?: number | null;
}

export interface ListMyNotificationsResponse {
  notifications: NotificationInboxItem[];
  unreadCount: number;
}

export interface UnreadNotificationCountResponse {
  unreadCount: number;
}

export type NotificationFilters = {
  search?: string;
  type?: string;
  isUnread?: boolean;
  scope?: NotificationScope;
};
