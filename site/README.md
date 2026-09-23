# Frontier callback page

This directory contains the static HTTPS callback page for WOLPERTINGER's Frontier sign-in flow. The GitHub Pages workflow publishes only this directory.

The registered callback URL is:

https://keilerhirsch.github.io/wolpertinger/oauth/callback/

The callback is served directly from `oauth/callback/index.html` and loads its CSS and JavaScript with relative asset paths. It receives Frontier's one-time authorization code and state, removes the query from the visible address, and prepares a link to `wolpertinger://frontier/oauth`.

The page does not exchange tokens, store credentials, load third-party scripts, or ask for a Frontier password. The Windows app must register the URI scheme and perform its own state and PKCE validation and code exchange.
