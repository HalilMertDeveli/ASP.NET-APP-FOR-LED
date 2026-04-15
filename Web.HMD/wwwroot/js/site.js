document.addEventListener("DOMContentLoaded", () => {
    const initGlobalLedBackground = () => {
        const bg = document.getElementById("globalLedBg");
        if (!bg) return;

        const dotSize = 7;
        const gap = 14;
        const influenceRadius = 220;
        const influenceRadiusSquared = influenceRadius * influenceRadius;
        const maxDots = 2200;
        const rgbPalette = [
            [255, 64, 64],
            [64, 255, 96],
            [64, 140, 255]
        ];
        const dots = [];
        let mouseX = window.innerWidth / 2;
        let mouseY = window.innerHeight / 2;
        let targetMouseX = mouseX;
        let targetMouseY = mouseY;

        const buildGrid = () => {
            bg.innerHTML = "";
            dots.length = 0;

            const w = bg.clientWidth;
            const h = bg.clientHeight;
            const cols = Math.max(1, Math.floor(w / (dotSize + gap)));
            const rows = Math.max(1, Math.floor(h / (dotSize + gap)));
            const total = cols * rows;
            const step = Math.max(1, Math.ceil(total / maxDots));

            for (let r = 0; r < rows; r++) {
                for (let c = 0; c < cols; c++) {
                    const index = r * cols + c;
                    if (index % step !== 0) continue;

                    const dot = document.createElement("span");
                    dot.className = "global-led-dot";
                    const x = c * (dotSize + gap);
                    const y = r * (dotSize + gap);
                    dot.style.left = `${x}px`;
                    dot.style.top = `${y}px`;
                    bg.appendChild(dot);
                    dots.push({ el: dot, x: x + dotSize * 0.5, y: y + dotSize * 0.5, colorIndex: index % 3 });
                }
            }
        };

        const animate = () => {
            // Smooth and fast cursor tracking without jitter.
            mouseX += (targetMouseX - mouseX) * 0.28;
            mouseY += (targetMouseY - mouseY) * 0.28;

            for (let i = 0; i < dots.length; i++) {
                const dot = dots[i];
                const dx = mouseX - dot.x;
                const dy = mouseY - dot.y;
                const distanceSquared = dx * dx + dy * dy;
                const intensity = distanceSquared < influenceRadiusSquared
                    ? 1 - distanceSquared / influenceRadiusSquared
                    : 0;

                if (intensity > 0) {
                    const alpha = 0.2 + intensity * 0.8;
                    const [r, g, b] = rgbPalette[dot.colorIndex];
                    dot.el.style.background = `rgba(${r}, ${g}, ${b}, ${alpha})`;
                    dot.el.style.boxShadow = `0 0 ${8 + intensity * 18}px rgba(${r}, ${g}, ${b}, ${0.5 + intensity * 0.45})`;
                    dot.el.style.transform = `scale(${1 + intensity * 0.95})`;
                } else {
                    dot.el.style.background = "rgba(51, 65, 85, 0.28)";
                    dot.el.style.boxShadow = "none";
                    dot.el.style.transform = "scale(1)";
                }
            }

            requestAnimationFrame(animate);
        };

        document.addEventListener("mousemove", (e) => {
            const rect = bg.getBoundingClientRect();
            targetMouseX = e.clientX - rect.left;
            targetMouseY = e.clientY - rect.top;
        }, { passive: true });

        window.addEventListener("resize", buildGrid);

        buildGrid();
        animate();
    };

    initGlobalLedBackground();

    const widget = document.getElementById("liveSupportWidget");
    const toggle = document.getElementById("liveSupportToggle");
    const panel = document.getElementById("liveSupportPanel");
    const messages = document.getElementById("liveSupportMessages");
    const form = document.getElementById("liveSupportForm");
    const input = document.getElementById("liveSupportInput");
    const suggestionButtons = document.querySelectorAll(".suggestion-btn");

    if (!widget || !toggle || !panel || !messages || !form || !input) return;

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
            { key: "pValue", question: "1) Panelin P degerini girin. (Orn: p2.5)" },
            { key: "chipsetValue", question: "2) Chipset degerini girin. (Orn: 1065s)" },
            { key: "decoderValue", question: "3) Decoding/decoder degerini girin. (Orn: 2012)" }
        ]
    };

    const appendMessage = (text, type) => {
        const msg = document.createElement("div");
        msg.className = `msg ${type}`;
        msg.textContent = text;
        messages.appendChild(msg);
        messages.scrollTop = messages.scrollHeight;
    };

    const appendFileLinkMessage = (file) => {
        const msg = document.createElement("div");
        msg.className = "msg bot";

        const fileType = (file.fileType || "").toUpperCase();
        const fileName = file.fileName || "Dosya";
        const fileUrl = file.fileUrl || "#";

        const prefix = document.createElement("span");
        prefix.textContent = `${fileType} | `;

        const link = document.createElement("a");
        link.href = fileUrl;
        link.textContent = fileName;
        link.className = "text-info fw-semibold";
        link.setAttribute("download", fileName);
        link.setAttribute("target", "_blank");
        link.setAttribute("rel", "noopener noreferrer");

        msg.appendChild(prefix);
        msg.appendChild(link);
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
            `P: ${panelFlow.data.pValue}`,
            `Chipset: ${panelFlow.data.chipsetValue}`,
            `Decoder: ${panelFlow.data.decoderValue}`
        ].join(" | ");

        appendMessage(`Bilgileri aldim. Arama yapiyorum... (${summary})`, "bot");

        try {
            const query = new URLSearchParams({
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

            appendMessage(`Eslesen ${files.length} dosya bulundu (rcvp/hex):`, "bot");
            files.forEach((file) => {
                appendFileLinkMessage(file);
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
