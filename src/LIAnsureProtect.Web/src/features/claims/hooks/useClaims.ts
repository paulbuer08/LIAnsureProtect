import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  fileClaim,
  getClaimDetail,
  listClaimablePolicies,
  listMyClaims,
  respondToInformationRequest,
  setClaimedAmount,
  uploadClaimDocuments,
} from "../api/claimsApi";
import type {
  FileClaimRequest,
  RespondToInformationRequestRequest,
  SetClaimedAmountRequest,
} from "../types";

export const myClaimsQueryKey = ["claims", "mine"];

export function claimDetailQueryKey(claimId: string) {
  return ["claims", "detail", claimId];
}

export function useMyClaims() {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: myClaimsQueryKey,
    queryFn: async () => listMyClaims(await getAccessTokenSilently()),
  });
}

export function useClaimDetail(claimId: string) {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: claimDetailQueryKey(claimId),
    queryFn: async () => getClaimDetail(await getAccessTokenSilently(), claimId),
  });
}

export function useClaimablePolicies() {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: ["claims", "policy-options"],
    queryFn: async () => listClaimablePolicies(await getAccessTokenSilently()),
  });
}

export function useFileClaim() {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: FileClaimRequest) =>
      fileClaim(await getAccessTokenSilently(), request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: myClaimsQueryKey });
    },
  });
}

export function useClaimantActions(claimId: string) {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  function invalidateDetail() {
    void queryClient.invalidateQueries({
      queryKey: claimDetailQueryKey(claimId),
    });
  }

  const respond = useMutation({
    mutationFn: async (input: {
      informationRequestId: string;
      request: RespondToInformationRequestRequest;
    }) =>
      respondToInformationRequest(
        await getAccessTokenSilently(),
        claimId,
        input.informationRequestId,
        input.request,
      ),
    onSuccess: invalidateDetail,
  });

  const declareClaimedAmount = useMutation({
    mutationFn: async (request: SetClaimedAmountRequest) =>
      setClaimedAmount(await getAccessTokenSilently(), claimId, request),
    onSuccess: invalidateDetail,
  });

  const uploadDocuments = useMutation({
    mutationFn: async (input: { kind: string; files: File[] }) =>
      uploadClaimDocuments(
        await getAccessTokenSilently(),
        claimId,
        input.kind,
        input.files,
      ),
    onSuccess: invalidateDetail,
  });

  return { respond, declareClaimedAmount, uploadDocuments };
}
