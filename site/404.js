(() => {
  "use strict";

  const expectedPath = "/wolpertinger/oauth/callback/";
  const status = document.getElementById("status");
  const continueLink = document.getElementById("continue");

  if (window.location.pathname !== expectedPath) {
    if (status) {
      status.textContent = "This page could not be found.";
    }
    if (continueLink) {
      continueLink.hidden = true;
      continueLink.removeAttribute("href");
    }
    return;
  }

  if (!status || !continueLink) {
    return;
  }

  status.textContent = "Preparing the secure handoff to the app…";

  const callbackScript = document.createElement("script");
  callbackScript.src = "/WOLPERTINGER/oauth/callback/callback.js";
  callbackScript.addEventListener("error", () => {
    status.textContent = "The callback handler could not be loaded. Return to WOLPERTINGER and try again.";
  }, { once: true });
  document.head.append(callbackScript);
})();
