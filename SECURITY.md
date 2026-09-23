# Security Policy

WOLPERTINGER is pre-alpha software and has no supported end-user release yet.

## Reporting a vulnerability

Please do not publish secrets, private account data, active credentials, or weaponized exploit details in a public issue.

If GitHub's private **Report a vulnerability** option is available for this repository, use it for sensitive reports. If it is not available, open a minimal public issue requesting a private security contact path, without including exploit details or sensitive data.

For non-sensitive hardening suggestions, ordinary GitHub issues are appropriate.

## Scope

Useful reports include vulnerabilities or trust-boundary failures involving:

- Frontier OAuth / callback handling;
- local evidence or replay integrity;
- the .NET ↔ trusted-kernel process boundary;
- protocol / schema validation;
- package or update integrity;
- privilege, path, or secret handling.

Please include the affected revision, reproduction conditions, and the minimum evidence needed to verify the problem. Do not include real Frontier credentials, tokens, raw private profile responses, or unrelated personal data.
