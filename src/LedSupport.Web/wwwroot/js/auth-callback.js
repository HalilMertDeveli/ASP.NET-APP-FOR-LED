(() => {
  const status = document.querySelector("[data-callback-status]");
  if (!status || !window.supabase) return;

  const url = status.dataset.url;
  const key = status.dataset.key;
  if (!url || !key) {
    status.textContent = "Supabase yapılandırması eksik.";
    return;
  }

  const params = new URLSearchParams(window.location.search);
  const oauthError = params.get("error_description") || params.get("error");
  if (oauthError) {
    status.textContent = oauthError;
    return;
  }

  const client = window.supabase.createClient(url, key);

  const complete = async (session) => {
    if (!session?.access_token) {
      status.textContent = "Google oturumu alınamadı. Tekrar giriş yapın.";
      return;
    }

    const response = await fetch("/Giris?handler=Complete", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        accessToken: session.access_token,
        refreshToken: session.refresh_token || ""
      })
    });

    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      status.textContent = payload.error || "Oturum doğrulanamadı.";
      return;
    }

    window.location.replace(payload.redirect || "/Hesap");
  };

  const start = async () => {
    const { data, error } = await client.auth.getSession();
    if (data?.session) {
      await complete(data.session);
      return;
    }

    if (error) {
      status.textContent = error.message;
      return;
    }

    const { data: listener } = client.auth.onAuthStateChange(async (event, session) => {
      if (session && (event === "SIGNED_IN" || event === "INITIAL_SESSION")) {
        listener.subscription.unsubscribe();
        await complete(session);
      }
    });

    window.setTimeout(() => {
      status.textContent = "Giriş tamamlanamadı. Google ile tekrar deneyin.";
    }, 8000);
  };

  start();
})();
