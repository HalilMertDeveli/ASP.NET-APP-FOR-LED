document.addEventListener("DOMContentLoaded", () => {
    const widget = document.getElementById("liveSupportWidget");
    const toggle = document.getElementById("liveSupportToggle");
    const panel = document.getElementById("liveSupportPanel");
    const messages = document.getElementById("liveSupportMessages");
    const form = document.getElementById("liveSupportForm");
    const input = document.getElementById("liveSupportInput");
    const suggestionButtons = document.querySelectorAll(".suggestion-btn");

    if (!widget || !toggle || !panel || !messages || !form || !input) {
        return;
    }

    const botResponses = {
        "servis sureci kac gun suruyor?": "Paket tipine gore 4 saat ile 3 is gunu arasinda tamamliyoruz.",
        "odeme secenekleri nelerdir?": "Havale, kredi karti ve kurumsal musteriye ozel fatura secenekleri mevcut.",
        "canli temsilciye baglanmak istiyorum.": "Canli temsilciye baglaniyorsunuz... Ortalama baglanti suresi 1-2 dakika.",
        default: "Mesajinizi aldim. Ekibimiz size en kisa surede donus saglayacak."
    };

    const panelFlow = {
        isActive: false,
        currentStepIndex: 0,
        data: {},
        steps: [
            { key: "panelType", question: "1) Hangi panel turu kullaniyorsunuz? SMD mi COB mi?" },
            { key: "chipsetValue", question: "2) Panel arkasindaki etikette yazan chipset degerini girin." },
            { key: "decoderValue", question: "3) Panelin arkasindaki decoder degerini girin." },
            { key: "pValue", question: "4) Panelin P degerini girin. (Orn: P2.5)" }
        ]
    };

    const appendMessage = (text, type) => {
        const msg = document.createElement("div");
        msg.className = `msg ${type}`;
        msg.textContent = text;
        messages.appendChild(msg);
        messages.scrollTop = messages.scrollHeight;
    };

    const normalizeText = (text) => text.trim().toLowerCase();

    const shouldStartPanelFlow = (text) => {
        const normalized = normalizeText(text);
        return normalized.includes("dosya") || normalized.includes("panel") || normalized.includes("chipset") || normalized.includes("decoder");
    };

    const resetPanelFlow = () => {
        panelFlow.isActive = false;
        panelFlow.currentStepIndex = 0;
        panelFlow.data = {};
    };

    const startPanelFlow = () => {
        resetPanelFlow();
        panelFlow.isActive = true;
        appendMessage(
            "Tabii, dosya iletimi icin once panel bilgilerini sirayla toplayalim. Istediginiz anda 'iptal' yazarak cikabilirsiniz.",
            "bot"
        );
        appendMessage(panelFlow.steps[0].question, "bot");
    };

    const completePanelFlow = async () => {
        const summary = [
            `Panel: ${panelFlow.data.panelType}`,
            `Chipset: ${panelFlow.data.chipsetValue}`,
            `Decoder: ${panelFlow.data.decoderValue}`,
            `P: ${panelFlow.data.pValue}`
        ].join(" | ");

        appendMessage(`Bilgileri aldim. Arama yapiyorum... (${summary})`, "bot");

        try {
            const query = new URLSearchParams({
                panelType: panelFlow.data.panelType || "",
                chipsetValue: panelFlow.data.chipsetValue || "",
                decoderValue: panelFlow.data.decoderValue || "",
                pValue: panelFlow.data.pValue || ""
            });

            const response = await fetch(`/Support/GetPanelFiles?${query.toString()}`);
            if (!response.ok) {
                appendMessage("Dosya sorgusunda bir hata olustu. Lutfen tekrar deneyin.", "bot");
                resetPanelFlow();
                return;
            }

            const files = await response.json();
            if (!Array.isArray(files) || files.length === 0) {
                appendMessage("Bu bilgilere uygun dosya bulunamadi.", "bot");
                resetPanelFlow();
                return;
            }

            appendMessage(`Eslesen ${files.length} dosya bulundu. Indirme linkleri:`, "bot");
            files.forEach((file) => {
                const text = `${file.fileName || "Dosya"} -> ${file.fileUrl || "#"}`;
                appendMessage(text, "bot");
            });
        } catch (error) {
            appendMessage("Sistem hatasi olustu. Lutfen biraz sonra tekrar deneyin.", "bot");
        }

        resetPanelFlow();
    };

    const handlePanelFlowInput = (value) => {
        const normalized = normalizeText(value);
        if (normalized === "iptal") {
            appendMessage("Panel bilgi toplama islemi iptal edildi.", "bot");
            resetPanelFlow();
            return;
        }

        const currentStep = panelFlow.steps[panelFlow.currentStepIndex];
        if (!currentStep) {
            resetPanelFlow();
            return;
        }

        panelFlow.data[currentStep.key] = value;
        panelFlow.currentStepIndex += 1;

        if (panelFlow.currentStepIndex >= panelFlow.steps.length) {
            completePanelFlow();
            return;
        }

        const nextStep = panelFlow.steps[panelFlow.currentStepIndex];
        appendMessage(nextStep.question, "bot");
    };

    const botReply = (question) => {
        if (panelFlow.isActive) {
            setTimeout(() => handlePanelFlowInput(question), 300);
            return;
        }

        if (shouldStartPanelFlow(question)) {
            setTimeout(() => startPanelFlow(), 300);
            return;
        }

        const key = normalizeText(question);
        const response = botResponses[key] || botResponses.default;
        setTimeout(() => appendMessage(response, "bot"), 500);
    };

    const openWidget = () => {
        widget.classList.add("open");
        panel.setAttribute("aria-hidden", "false");
    };

    const toggleWidget = () => {
        const isOpen = widget.classList.toggle("open");
        panel.setAttribute("aria-hidden", isOpen ? "false" : "true");
    };

    toggle.addEventListener("click", toggleWidget);
    widget.addEventListener("mouseenter", openWidget);

    form.addEventListener("submit", (e) => {
        e.preventDefault();
        const value = input.value.trim();
        if (!value) return;
        appendMessage(value, "user");
        input.value = "";
        botReply(value);
    });

    suggestionButtons.forEach((button) => {
        button.addEventListener("click", () => {
            const question = button.getAttribute("data-question");
            if (!question) return;
            appendMessage(question, "user");
            botReply(question);
        });
    });
});
