---
area: auth
owners:
  - frontend
status: active
depends_on:
  - specs/00-foundations.spec.md
---

## Server-Side Auth & Workspace Resolution

- [X] AUTH-001: `FireblocksAuthenticationMiddleware` resolves the per-request `WorkspaceContext` and enforces API-key auth for Fireblocks-compatible endpoints. It short-circuits (passes through with no auth) for `OPTIONS`, paths containing `/health` or `/swagger`, and paths starting with `/supported_assets` or `/transactions/estimate_fee`. For `/admin/*` paths: `/admin/workspaces` is always anonymous; otherwise it reads the `X-Workspace-Id` header and, if the workspace exists and is not soft-deleted, sets `WorkspaceContext.WorkspaceId` to that value; if missing or unknown, it falls back to the oldest non-deleted workspace by `CreatedAt`. For `/vault/*` and `/transactions/*` (Fireblocks-compatible endpoints) it requires either an `X-API-Key` header or an `Authorization` header — missing both returns 401 with body `{"message":"Unauthorized","code":401}` (note: not the Fireblocks `ErrorResponse` shape because the middleware short-circuits before the error mapper). When `X-API-Key` is supplied it is looked up in `ApiKeys` (joined to a non-deleted workspace) and an unknown key returns the same 401; on success `WorkspaceContext.WorkspaceId`/`ApiKey` are set. Bearer-only (no `X-API-Key`) is accepted with no JWT validation (test-mode) and the workspace falls back to the oldest non-deleted workspace. Source: `Waterblocks.Api/Middleware/FireblocksAuthenticationMiddleware.cs:1-138`. Confidence: high.

## Client-Side Identity

- [X] AUTH-UI-001: The `LoginGate` component renders a full-screen email-capture card when no identity is stored; the form requires a non-empty trimmed email containing `@` (else displays an inline error), and invokes `onLogin(email)` with the trimmed value on submit. Source: `waterblocks-admin/src/components/LoginGate.tsx:7-50`. Confidence: high.
- [X] AUTH-UI-002: The `useCurrentUser` hook persists the operator's email in `localStorage` under the key `currentUserEmail`, exposes `login`/`logout` callbacks (trim-on-store, clear-on-logout), and reports `isLoggedIn` as `email.length > 0`; storage failures are silently swallowed so private-mode browsers still work in-memory. Source: `waterblocks-admin/src/hooks/useCurrentUser.ts:3-44`. Confidence: high.
- [X] AUTH-UI-003: The captured operator email is forwarded as `initiatedBy` on transaction-create requests, providing audit attribution for admin-API-initiated transactions. Source: `waterblocks-admin/src/pages/TransactionsPage.tsx:436-436`. Confidence: high.
