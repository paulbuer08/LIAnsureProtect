export interface NotificationInboxItem {
  notificationId: string;
  type: string;
  title: string;
  subjectReferenceType: string;
  subjectReferenceId: string;
  attributes: Record<string, string>;
  occurredAtUtc: string;
  isRead: boolean;
  readAtUtc: string | null;
}

export interface ListMyNotificationsResponse {
  notifications: NotificationInboxItem[];
  unreadCount: number;
}
