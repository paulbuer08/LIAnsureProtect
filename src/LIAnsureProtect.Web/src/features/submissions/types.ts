export type CreateSubmissionRequest = {
  applicantName: string;
  applicantEmail: string;
  companyName: string;
};

export type CreateSubmissionResponse = {
  submissionId: string;
  status: string;
};
