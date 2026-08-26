(() => {
  const root = document.querySelector("[data-chat]");
  if (!root || !window.supabase) return;

  const url = root.dataset.url;
  const key = root.dataset.key;
  const token = root.dataset.token;
  const conversationId = root.dataset.conversation;
  const log = root.querySelector("[data-chat-log]");
  const form = root.querySelector("[data-chat-form]");
  if (!url || !key || !conversationId || !log) return;

  const client = window.supabase.createClient(url, key);
  const known = new Set([...log.querySelectorAll("[data-id]")].map((el) => el.getAttribute("data-id")));

  const addBubble = (row) => {
    if (!row || known.has(row.id)) return;
    known.add(row.id);
    const empty = log.querySelector(".chat-empty");
    if (empty) empty.remove();
    const mine = row.sender_role === root.dataset.role;
    const article = document.createElement("article");
    article.className = "chat-bubble " + (mine ? "is-mine" : "is-theirs");
    article.dataset.id = row.id;
    const p = document.createElement("p");
    p.textContent = row.body || "";
    const time = document.createElement("time");
    const dt = row.created_at ? new Date(row.created_at) : new Date();
    time.textContent = dt.toLocaleString("tr-TR", {
      day: "2-digit",
      month: "2-digit",
      hour: "2-digit",
      minute: "2-digit"
    });
    article.append(p, time);
    log.append(article);
    log.scrollTop = log.scrollHeight;
  };

  const start = async () => {
    const { data } = await client.auth.getSession();
    const access = data?.session?.access_token || token;
    if (access) {
      await client.realtime.setAuth(access);
    }

    client
      .channel("messages:" + conversationId)
      .on(
        "postgres_changes",
        {
          event: "INSERT",
          schema: "public",
          table: "messages",
          filter: "conversation_id=eq." + conversationId
        },
        (payload) => addBubble(payload.new)
      )
      .subscribe();

    log.scrollTop = log.scrollHeight;
  };

  if (form) {
    let sending = false;
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      if (sending) return;
      const textarea = form.querySelector("textarea");
      const button = form.querySelector("button[type=submit]");
      const text = textarea ? textarea.value.trim() : "";
      if (!text) return;
      sending = true;
      if (button) button.disabled = true;
      try {
        const response = await fetch("/api/conversations/" + conversationId + "/messages", {
          method: "POST",
          credentials: "same-origin",
          headers: {
            "Content-Type": "application/json",
            Accept: "application/json"
          },
          body: JSON.stringify({ body: text })
        });
        if (response.status === 401) {
          window.location.href = "/Giris";
          return;
        }
        if (!response.ok) {
          throw new Error("send-failed");
        }
        const sent = await response.json();
        addBubble({
          id: sent.id,
          body: sent.body,
          sender_role: sent.senderRole,
          created_at: sent.createdAt
        });
        if (textarea) textarea.value = "";
      } catch {
        form.dataset.sendError = "1";
      } finally {
        sending = false;
        if (button) button.disabled = false;
      }
    });
  }

  start();
})();
