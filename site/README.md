# Frontier callback page

This directory contains the static HTTPS callback page for the WOLPERTINGER Frontier sign-in flow. The GitHub Pages workflow uploads this directory and no repository documentation or build output.

Frontier's Developer Zone currently saves the registered endpoint with the repository path lowercased:

https://keilerhirsch.github.io/wolpertinger/oauth/callback/

The project Pages site is published under the uppercase repository path, /WOLPERTINGER/. The root 404 page is designed to load the existing callback handler only when the browser path is exactly /wolpertinger/oauth/callback/. It loads the CSS and handler from the case-correct published route. Other missing paths remain not found.

GitHub Pages serves custom 404 content with a 404 status. This fallback is only a candidate until the exact lowercase URL is checked live and its callback content is observed in a browser. Client activation alone does not prove the redirect works.

The registered HTTPS redirect and the Windows application callback are separate values. The callback page receives Frontier's one-time code and state, removes the query from the visible address, and asks the user to open the registered WOLPERTINGER URI scheme. It does not perform token exchange, store credentials, load third-party scripts, or request Frontier passwords. The application must keep PKCE verification and state validation local and exchange the code using the exact registered HTTPS redirect URI.

The application callback URI in callback.js is currently a local prototype value and must match the package manifest before this page is used for sign-in.