# Frontier callback page

This directory contains only the static HTTPS callback page for the WOLPERTINGER Frontier sign-in flow. The GitHub Pages workflow uploads this directory and no repository documentation or build output.

Candidate project-site URL:

```text
https://keilerhirsch.github.io/wolpertinger/oauth/callback/
```

The URL is not active until GitHub Pages is configured and the workflow is run from `main`. Frontier must accept this exact redirect URI before it is used for sign-in.

The registered HTTPS redirect and the Windows application callback are separate values. This page receives Frontier's one-time `code` and `state`, removes the query from the visible address, and asks the user to open the registered WOLPERTINGER URI scheme. It does not perform token exchange, store credentials, load third-party scripts, or request Frontier passwords. The application must keep PKCE verification and state validation local and exchange the code using the exact registered HTTPS redirect URI.

The application callback URI in `callback.js` is currently a local prototype value and must match the package manifest before this page is published.
