(() => {
  const btn = document.querySelector("[data-google-login]");
  if (!btn || !window.supabase) return;

  const url = btn.dataset.url;
  const key = btn.dataset.key;
  const status = document.querySelector("[data-auth-status]");
  if (!url || !key) {
    if (status) status.textContent = "Google girişi yapılandırılmamış.";
    return;
  }

  btn.addEventListener("click", async () => {
    btn.disabled = true;
    if (status) status.textContent = "Google’a yönlendiriliyorsunuz…";
    const client = window.supabase.createClient(url, key);
    const { error } = await client.auth.signInWithOAuth({
      provider: "google",
      options: {
        redirectTo: window.location.origin + "/Giris/Callback",
        queryParams: { prompt: "select_account" }
      }
    });
    if (error) {
      btn.disabled = false;
      if (status) status.textContent = error.message || "Google girişi başlatılamadı.";
    }
  });
})();
