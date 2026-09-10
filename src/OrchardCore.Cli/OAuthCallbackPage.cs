namespace OrchardCore.Cli;

internal static class OAuthCallbackPage
{
    public static string Render(bool authorizationReceived)
    {
        var title = authorizationReceived ? "Authorization received" : "Authorization not completed";
        var status = authorizationReceived ? "Browser step complete" : "Login interrupted";
        var message = authorizationReceived
            ? "Your authorization has been sent to the Orchard Core CLI."
            : "Access was denied or the authorization request could not be completed.";
        var nextStep = authorizationReceived
            ? "Check your terminal to confirm that login completed."
            : "Run <code>oc login</code> in your terminal to try again.";

        // All interpolated content is fixed text. Never include callback parameters,
        // codes, tokens, or identity-provider error descriptions in this page.
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="color-scheme" content="light dark">
              <title>{{title}} · Orchard Core CLI</title>
              <style>
                :root { color-scheme: light dark; --bg: #f5f7f5; --surface: #fff; --text: #20352e; --muted: #5d6f67; --line: #dee7e0; --accent: #216b49; --tint: #edf5ef; }
                * { box-sizing: border-box; }
                body { margin: 0; min-height: 100svh; padding: 48px 24px; display: grid; place-items: center; background: var(--bg); color: var(--text); font: 16px/1.6 system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
                .page { width: 100%; max-width: 560px; }
                .brand { display: flex; align-items: center; gap: 12px; margin: 0 0 24px 4px; font-weight: 650; }
                .brand-mark { display: grid; place-items: center; width: 38px; height: 38px; border-radius: 11px; background: var(--accent); color: #fff; font: 600 18px ui-monospace, monospace; }
                .brand span:last-child { color: var(--muted); font-size: 13px; font-weight: 450; padding-left: 12px; border-left: 1px solid var(--line); }
                main { padding: 40px; border: 1px solid var(--line); border-radius: 20px; background: var(--surface); box-shadow: 0 12px 40px #20352e08; }
                .status { display: flex; align-items: center; gap: 8px; margin: 0 0 18px; color: var(--accent); font-size: 12px; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
                .status::before { content: ""; width: 7px; height: 7px; border-radius: 50%; background: currentColor; }
                h1 { margin: 0 0 14px; font-size: clamp(26px, 5vw, 34px); font-weight: 650; line-height: 1.2; letter-spacing: -.035em; }
                .message { margin: 0; color: var(--muted); }
                .next { display: flex; gap: 14px; padding: 20px; margin-top: 30px; background: var(--tint); border: 1px solid var(--line); border-radius: 12px; }
                .next svg { flex: 0 0 24px; margin-top: 3px; color: var(--accent); }
                h2 { margin: 0 0 4px; font-size: 15px; font-weight: 650; }
                .next p { margin: 0; color: var(--muted); font-size: 14px; }
                code { font: .95em ui-monospace, monospace; color: var(--text); }
                footer { margin-top: 22px; text-align: center; color: var(--muted); font-size: 13px; }
                @media (max-width: 480px) { body { padding: 28px 16px; } main { padding: 28px 24px; } .brand { gap: 9px; } .next { padding: 16px; } }
                @media (prefers-color-scheme: dark) { :root { --bg: #121a16; --surface: #1a2520; --text: #edf4ef; --muted: #b0c1b6; --line: #33463b; --accent: #8bd6aa; --tint: #22362a; } .brand-mark { background: #286d4a; } }
              </style>
            </head>
            <body>
              <div class="page">
                <header class="brand"><span class="brand-mark" aria-hidden="true">oc</span> Orchard Core <span>Command-line interface</span></header>
                <main>
                  <p class="status">{{status}}</p>
                  <h1>{{title}}</h1>
                  <p class="message">{{message}}</p>
                  <section class="next" aria-labelledby="next-step">
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="2" y="4" width="20" height="16" rx="3"/><path d="m6 9 3 3-3 3m7 0h4"/></svg>
                    <div><h2 id="next-step">Return to your terminal</h2><p>{{nextStep}}</p></div>
                  </section>
                </main>
                <footer>You can close this browser tab.</footer>
              </div>
            </body>
            </html>
            """;
    }

    public static Task WriteAsync(TextWriter writer, bool authorizationReceived) => writer.WriteAsync(
        "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\n"
        + "Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; frame-ancestors 'none'\r\n"
        + "Referrer-Policy: no-referrer\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n"
        + Render(authorizationReceived));
}
