export type CreateSubmissionRequest = {
  applicantName: string;
  applicantEmail: string;
  companyName: string;
};

export type CreateSubmissionResponse = {
  submissionId: string;
  status: string;
};

export type SubmissionListItem = {
  submissionId: string;
  applicantName: string;
  applicantEmail: string;
  companyName: string;
  status: string;
  createdAtUtc: string;
};

export type ListSubmissionsResponse = {
  submissions: SubmissionListItem[];
};

export type SubmissionDetailResponse = SubmissionListItem;
