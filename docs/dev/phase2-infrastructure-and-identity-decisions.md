# Phase 2 Infrastructure & Identity — Decisions and Setup Log

> **Living decisions record.** Captures the infrastructure, DNS, edge, and identity decisions made
> before/at the start of Phase 2 (M45+), plus the concrete external-account setup performed
> (Cloudflare domain, Auth0). Future sessions must honor these decisions when planning M45–M50.
> Companion to the [Production Transformation Roadmap](production-transformation-roadmap.md).

## 0. How the two tracks run in parallel

Development proceeds on **two independent tracks at the same time**:

| Track | What | Branch model |
|---|---|---|
| **Phase 2 — Terraform/AWS infrastructure (M45+)** | Provision real AWS (foundation → data → compute → edge), done manually by the owner with assistant help | Normal trunk flow: `feat/milestone-45-…` off `main` → PR into **`main`** → squash-merge |
| **Phase 3 — Claims bounded context** | The full Claims/FNOL context, built autonomously in a **separate session** | Child branches `feat/claims/cm1..cm8` → PR into the long-lived parent **`feat/claims-context`** (never `main`) until Phase 2 is done, then the parent merges to `main` |

The tracks have **no dependency on each other** (Claims is application code under `src/Modules/Claims`;
Phase 2 is infrastructure under a new `infra/` tree), so they parallelize cleanly. The Claims session
runs in its own git worktree so it never collides with the owner's `main` working folder.

**Rule:** do not merge `feat/claims-context` into `main` before Phase 2 is complete.

## 1. Edge architecture — AWS-native; Cloudflare is registrar-only

**Decision:** the production edge (DNS, CDN, WAF, DDoS, TLS) is **AWS-native**, not Cloudflare.

Cloudflare's CDN/WAF/DDoS/TLS features **overlap** with — are redundant to — the AWS edge stack.
You pick **one** edge; running both in series (Cloudflare → CloudFront) adds double caching, two WAFs,
latency, and cost for no benefit.

| Job | Chosen (AWS-native) | Redundant Cloudflare feature (NOT used) |
|---|---|---|
| DNS | **Route 53** | Cloudflare DNS |
| CDN | **CloudFront** | Cloudflare CDN |
| WAF | **AWS WAF** | Cloudflare WAF |
| DDoS | **AWS Shield** | Cloudflare DDoS |
| TLS certs | **ACM** | Cloudflare Universal SSL |
| Registrar | — | **Cloudflare Registrar** ✅ (kept — this is all we use Cloudflare for) |

**Why AWS-native:** this is an AWS-transformation portfolio project — the entire point of Phase 2 is
to demonstrate AWS (EKS, CloudFront, WAF, Terraform). Keeping the edge in AWS gives one Terraform-
managed, single-cloud story with native ALB/EKS/S3 integration.

**Cloudflare stays as the domain registrar only.** Registrar ≠ CDN — you can register at Cloudflare
and host DNS + edge in AWS. In Phase 2, **delegate the domain's nameservers to a Route 53 hosted zone**
(DNS-as-code in Terraform). Do **not** enable Cloudflare's CDN/WAF/Bot-Fight/Leaked-credential features
— those are the redundant part.

**Bake into the plans:** M45 (foundation) provisions the Route 53 hosted zone + NS delegation + ACM;
M47 (edge) provisions CloudFront + AWS WAF + Shield in front of the app origin.

## 2. Identity — Auth0 stays as the CIAM; Cloudflare is not an IdP

**Decision:** keep **Auth0** as the customer identity provider. The real future swap decision is
**Auth0 vs AWS Cognito** (a Phase-2 identity call), **not** Cloudflare.

- **Auth0 = CIAM / OIDC IdP:** runs login/signup, the user directory, MFA, and **issues the JWT the
  API validates** (audience `https://api.liansureprotect.local`, roles claim
  `https://liansureprotect.local/roles`). The app *delegates identity* to it (M6/M7).
- **Cloudflare is a different layer (edge/network) and is NOT an identity provider.** Its "Access /
  Zero Trust" product gates *internal* apps and **federates to** an IdP (it doesn't issue your app's
  customer tokens). So Auth0 and Cloudflare are complementary, not redundant.

> Summary of the two "redundancy" questions from this session:
> - **Auth0 vs Cloudflare → NOT redundant** (identity vs edge). Keep Auth0.
> - **Cloudflare CDN/WAF vs CloudFront/AWS WAF → REDUNDANT** (same layer). Use AWS; keep Cloudflare
>   as registrar only.

## 3. Domain — `liansureprotect.com` (Cloudflare Registrar)

Purchased on Cloudflare Registrar (~$10/yr, expires 2027-07-06). Registrar-only per §1.

**Renaming later:** a registration string is fixed — you can't "rename" it, but switching is cheap
(buy a new domain, repoint config, let the old lapse). The domain lives only in configuration
(Auth0 custom domain, `VITE_AUTH0_DOMAIN`, API `Authentication:Authority`), never in code, so a later
switch is a ~1-hour config change with zero code changes.

### Security setup completed this session
- **Auto-renew ON** (the #1 way to lose a domain is accidental expiry).
- **Account 2FA** enabled — Cloudflare moved it to **Profile → Access Management** (not the Settings
  tab; the old "Authentication" section was renamed).
- **DNSSEC** enabled (DNS → Settings).
- **Registrar Lock + WHOIS privacy** — on by default; verified.
- **Anti-spoofing email records** via Cloudflare's "domain is not used to send email" one-click bundle:

  | Type | Name | Content |
  |---|---|---|
  | TXT | `liansureprotect.com` | `v=spf1 -all` |
  | TXT | `*._domainkey` | `v=DKIM1; p=` |
  | TXT | `_dmarc` | `v=DMARC1; p=reject; sp=reject; adkim=s; aspf=s;` |

  (Chosen over the individual SPF/DMARC creators, which default to weaker `~all` / `p=none` and are
  for domains that *do* send mail.)

### Temporary — will change in Phase 2
These DNS records currently live at Cloudflare. When Phase 2 delegates DNS to Route 53, they **move to
Route 53** (re-entered there; DNSSEC re-done on the Route 53 side with the DS record placed back at the
Cloudflare registrar). When real email is set up (below), SPF relaxes to include the provider and real
DKIM keys are added.

## 4. Auth0 setup log & the email-deliverability plan

### Roles claim (working)
- Post-Login Action sets the claim on **both** `accessToken` and `idToken` at key
  `https://liansureprotect.local/roles`; the API reads exactly that (`Authentication:RoleClaimType`).
  The frontend does not read roles from the token today (role enforcement is server-side 403), but
  setting the idToken claim future-proofs role-aware UI (e.g. the Claims workbench).
- **Critical gotcha:** the Action must be **deployed AND dragged into the Login flow**
  (Actions → Triggers → post-login). An Action only in the Library does nothing → every API call
  returns 403 even with a perfect script. First thing to check when debugging 403-everywhere.

### Verification emails landing in spam — the accurate root cause
- **Real cause = shared-domain reputation, NOT missing authentication and NOT the tenant name.**
  Auth0 dev tenants send verification mail from `no-reply@auth0user.net` — a domain **shared by
  thousands of dev tenants**. It *does* have its own basic SPF/DKIM (the mail isn't failing auth),
  but it's a bulk shared sender with mediocre reputation, sending a generic transactional email to
  a brand-new recipient → Gmail plays it safe → spam. The random `dev-…` tenant display name is only
  a minor cosmetic signal, not the cause.
- **No conflict with your domain's DNS.** The restrictive SPF/DKIM/DMARC records on
  `liansureprotect.com` (§3) only govern mail claiming to be *from `@liansureprotect.com`*. Auth0's
  emails are from `@auth0user.net` — a different domain — so your records neither block nor affect
  them. "Nothing may send as `@liansureprotect.com`" and "Auth0 sends from `@auth0user.net`" are both
  true and independent. (This confused us mid-session; documented so it isn't re-derived.)
- **Cheap visual fix (done/enough for now):** Auth0 → Settings → General → set **Friendly Name**
  (`LIAnsureProtect`) + logo + support email so the sender/login *look* legitimate (doesn't change
  spam-foldering).
- **Real deliverability fix (deferred, optional):** custom email provider — **Amazon SES** (fits the
  AWS direction) or SendGrid — verify `liansureprotect.com`, add its SPF/DKIM, **relax SPF** to
  `include:` the provider (keep `p=reject`), set Auth0 Branding → Email Provider to send from
  `no-reply@liansureprotect.com`. Only then do the §3 records start mattering for Auth0 mail.
- **For testing now:** click the verify link once per persona from spam, and hit **"Not spam"** so
  Gmail inboxes the rest; or pre-verify the users. Don't let this block the walkthrough.

### Auth0 lifecycle triggers — what we use, and what's deferred (and why)
Only the **post-login roles Action** is in use. The other lifecycle hooks were evaluated and
**deliberately deferred** until a concrete consumer exists:

| Trigger | Verdict | Add it when… |
|---|---|---|
| `onExecutePostLogin` (roles claim) | **In use** ✅ | — (current) |
| `onContinuePostLogin` | **Not applicable now** — it only runs when the post-login Action does a **redirect** (`api.redirect.sendUserTo`); the roles Action has no redirect, so leave it commented out. | a redirect flow is added: ToS/consent gate, progressive profiling (collect missing profile fields), or a custom step-up-MFA page (the roadmap's step-up MFA, *if* built as a redirect flow). |
| **Pre User Registration** (`onExecutePreUserRegistration`) | **Deferred** — fires only on Database-connection self-signup, which we don't exercise (personas are created by hand). Not needed for security: RBAC already means a self-signup gets **no roles** → the API 403s everything, so nobody can self-grant Underwriter/Admin. | real self-service signup is enabled and you want domain allow/deny rules or a `pending_approval` tag. Roles are still assigned by an admin, never here. |
| **Post User Registration** (`onExecutePostUserRegistration`) | **Deferred** — async, side-effect only (can't change the flow). The app is stateless about identity (derives everything from the JWT `sub` via `ICurrentUser`; **no user/account table**), so there's nothing to sync. | the roadmap's **Accounts/Companies** bounded context exists and you want to provision an account profile / send a welcome (needs an M2M token in the Action's Secrets + a receiving API endpoint). |

**Guardrail to remember:** never set/grant roles in a pre/post-registration Action — roles come from
admin-assigned Auth0 RBAC and are read via `event.authorization.roles` in the post-login Action. This
keeps privileged roles (Underwriter, ClaimsAdjuster, Admin) un-self-grantable.

### Custom Auth0 domain (completed during the manual walkthrough)

`auth.liansureprotect.com` is now the local application's Auth0 issuer/domain. Auth0 manages the
certificate; Cloudflare temporarily hosts Auth0's supplied CNAME as **DNS only** so Auth0, rather
than Cloudflare's proxy, terminates TLS and can verify the record.

The switch must always happen on both sides because the token issuer must match:

- frontend: `VITE_AUTH0_DOMAIN=auth.liansureprotect.com`
- API: `Authentication__Authority=https://auth.liansureprotect.com/`

The original `dev-...us.auth0.com` tenant was not renamed. The custom domain is the stable public
alias in front of that tenant. When Phase 2 delegates authoritative DNS to Route 53, recreate the
same CNAME in Route 53 before changing nameservers; no Auth0 tenant change is required.

## 5. Manual UI walkthrough (hardening pass complete; policy journey next)

The owner completed the first hands-on Customer walkthrough before M45, following
[Running The App](../guides/running-the-app.md) (incl. the one-time Auth0 setup) and the
[Manual Testing Guide](../guides/manual-testing-guide.md) (five generic personas, four scenarios).
The walkthrough produced a role-aware navigation/Auth0 recovery/submission/quote hardening branch.
The complete decisions, implementation record, and approved follow-up plan are preserved in
[Customer/Broker Walkthrough Hardening and Policy Journey Plan](customer-broker-walkthrough-hardening-and-policy-journey-plan.md).
