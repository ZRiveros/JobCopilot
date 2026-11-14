// The only hand-written JavaScript in the app: two things Blazor Server cannot do from .NET.
window.jobCopilot = {
    // Copies text to the clipboard. navigator.clipboard only exists on HTTPS and localhost, and
    // a self-hosted app is often reached over plain HTTP on the LAN, so fall back to execCommand.
    copyText: async function (text) {
        try {
            if (navigator.clipboard && window.isSecureContext) {
                await navigator.clipboard.writeText(text);
                return true;
            }
        } catch {
            // Fall through to the legacy path.
        }

        const area = document.createElement("textarea");
        area.value = text;
        area.setAttribute("readonly", "");
        area.style.position = "fixed";
        area.style.opacity = "0";
        document.body.appendChild(area);
        area.select();

        let copied = false;
        try {
            copied = document.execCommand("copy");
        } catch {
            copied = false;
        }

        document.body.removeChild(area);
        return copied;
    }
};

// Firefox only starts a drag when the event carries data. The board itself tracks which card is
// being dragged on the server, so the payload is just the card id.
document.addEventListener("dragstart", function (event) {
    const card = event.target instanceof Element ? event.target.closest("[data-card-id]") : null;
    if (card && event.dataTransfer) {
        event.dataTransfer.setData("text/plain", card.getAttribute("data-card-id") || "");
        event.dataTransfer.effectAllowed = "move";
    }
});
