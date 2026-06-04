# ADR-002: Use React For The Frontend

## Status

Accepted

## Context

The app needs a modern browser UI for customers, brokers, underwriters, claims adjusters, and admins. The UI will include forms, dashboards, protected routes, document flows, queues, notifications, and review screens.

## Decision

Use React 19 with TypeScript and Vite.

Use:

- React Router for routing.
- TanStack Query for server state.
- React Hook Form and Zod for forms and validation.
- Zustand only for small client-side state.
- Vitest and React Testing Library for frontend tests.

Do not use Redux initially.

## Consequences

TanStack Query handles API fetching, caching, loading states, errors, refetching, and stale data better than a hand-rolled API state layer.

Zustand is reserved for small UI state such as sidebar state, theme, and temporary wizard progress.

Redux Toolkit can be added later only if client state becomes complex enough to justify it.
