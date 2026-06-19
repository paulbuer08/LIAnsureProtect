import { z } from "zod";

export const submissionIntakeSchema = z.object({
  applicantName: z.string().trim().min(1, "Applicant name is required."),
  applicantEmail: z
    .string()
    .trim()
    .min(1, "Applicant email is required.")
    .email("Enter a valid email address."),
  companyName: z.string().trim().min(1, "Company name is required."),
});

export type SubmissionIntakeFormValues = z.infer<
  typeof submissionIntakeSchema
>;
