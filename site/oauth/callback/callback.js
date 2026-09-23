(() => {
  "use strict";

  const applicationCallback = "wolpertinger://frontier/oauth";
  const status = document.getElementById("status");
  const continueLink = document.getElementById("continue");
  const responseSearch = window.location.search;
  const response = new URLSearchParams(responseSearch);
  const stateValues = response.getAll("state");
  const codeValues = response.getAll("code");
  const errorValues = response.getAll("error");

  window.history.replaceState(null, "", window.location.pathname);

  function showError(message) {
    status.textContent = message;
    continueLink.hidden = true;
    continueLink.removeAttribute("href");
  }

  if (responseSearch.length > 8192) {
    showError("The authorization response is invalid. Return to WOLPERTINGER and try again.");
    return;
  }

  if (stateValues.length !== 1 || !/^[A-Za-z0-9_-]{43}$/.test(stateValues[0])) {
    showError("The authorization response is incomplete. Return to WOLPERTINGER and try again.");
    return;
  }

  if (errorValues.length > 1 || codeValues.length > 1) {
    showError("The authorization response is invalid. Return to WOLPERTINGER and try again.");
    return;
  }

  const callback = new URL(applicationCallback);
  callback.searchParams.set("state", stateValues[0]);

  if (errorValues.length === 1) {
    callback.searchParams.set("error", "authorization_failed");
    status.textContent = "Frontier did not authorize this connection. Continue to return to WOLPERTINGER.";
  } else if (codeValues.length === 1 && codeValues[0].length > 0 && codeValues[0].length <= 4096) {
    callback.searchParams.set("code", codeValues[0]);
    status.textContent = "Authorization is ready. Continue to return to WOLPERTINGER.";
  } else {
    showError("The authorization response is incomplete. Return to WOLPERTINGER and try again.");
    return;
  }

  continueLink.href = callback.href;
  continueLink.hidden = false;
})();
