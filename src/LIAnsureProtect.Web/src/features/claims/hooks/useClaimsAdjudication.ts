import { useAuth0 } from "@auth0/auth0-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  acceptClaim,
  addClaimWorkNote,
  assignClaimToMe,
  closeClaim,
  denyClaim,
  getAdjudicationDetail,
  listAdjudicationQueue,
  releaseClaimAssignment,
  requestClaimInformation,
  setClaimReserve,
} from "../api/claimsApi";
import type {
  AcceptClaimRequest,
  AddWorkNoteRequest,
  DenyClaimRequest,
  RequestInformationRequest,
  SetReserveRequest,
} from "../types";

export const adjudicationQueueQueryKey = ["claims", "adjudication", "queue"];

export function adjudicationDetailQueryKey(claimId: string) {
  return ["claims", "adjudication", "detail", claimId];
}

export function useAdjudicationQueue() {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: adjudicationQueueQueryKey,
    queryFn: async () => listAdjudicationQueue(await getAccessTokenSilently()),
  });
}

export function useAdjudicationDetail(claimId: string | null) {
  const { getAccessTokenSilently } = useAuth0();

  return useQuery({
    queryKey: adjudicationDetailQueryKey(claimId ?? "none"),
    enabled: claimId !== null,
    queryFn: async () =>
      getAdjudicationDetail(await getAccessTokenSilently(), claimId ?? ""),
  });
}

export function useAdjudicationActions(claimId: string | null) {
  const { getAccessTokenSilently } = useAuth0();
  const queryClient = useQueryClient();

  // Success AND failure refetch: a 409 means someone else changed the claim first (the M44.5
  // pattern) — refetching shows the loser the truth instead of a stale panel.
  function refetchClaim() {
    void queryClient.invalidateQueries({ queryKey: adjudicationQueueQueryKey });
    if (claimId !== null) {
      void queryClient.invalidateQueries({
        queryKey: adjudicationDetailQueryKey(claimId),
      });
    }
  }

  async function token() {
    return getAccessTokenSilently();
  }

  const requiredClaimId = () => {
    if (claimId === null) {
      throw new Error("No claim is selected.");
    }
    return claimId;
  };

  const assign = useMutation({
    mutationFn: async () => assignClaimToMe(await token(), requiredClaimId()),
    onSettled: refetchClaim,
  });

  const release = useMutation({
    mutationFn: async () =>
      releaseClaimAssignment(await token(), requiredClaimId()),
    onSettled: refetchClaim,
  });

  const addNote = useMutation({
    mutationFn: async (request: AddWorkNoteRequest) =>
      addClaimWorkNote(await token(), requiredClaimId(), request),
    onSettled: refetchClaim,
  });

  const requestInformation = useMutation({
    mutationFn: async (request: RequestInformationRequest) =>
      requestClaimInformation(await token(), requiredClaimId(), request),
    onSettled: refetchClaim,
  });

  const setReserve = useMutation({
    mutationFn: async (request: SetReserveRequest) =>
      setClaimReserve(await token(), requiredClaimId(), request),
    onSettled: refetchClaim,
  });

  const accept = useMutation({
    mutationFn: async (request: AcceptClaimRequest) =>
      acceptClaim(await token(), requiredClaimId(), request),
    onSettled: refetchClaim,
  });

  const deny = useMutation({
    mutationFn: async (request: DenyClaimRequest) =>
      denyClaim(await token(), requiredClaimId(), request),
    onSettled: refetchClaim,
  });

  const close = useMutation({
    mutationFn: async () => closeClaim(await token(), requiredClaimId()),
    onSettled: refetchClaim,
  });

  return {
    assign,
    release,
    addNote,
    requestInformation,
    setReserve,
    accept,
    deny,
    close,
  };
}
