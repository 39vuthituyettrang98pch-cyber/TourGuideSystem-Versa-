(function () {
    const root = document.getElementById('dkAiChat');
    const toggle = document.getElementById('dkAiChatToggle');
    const close = document.getElementById('dkAiChatClose');
    const form = document.getElementById('dkAiChatForm');
    const input = document.getElementById('dkAiChatInput');
    const messages = document.getElementById('dkAiChatMessages');

    if (!root || !toggle || !form || !input || !messages) return;

    function getCurrentLanguage() {
        const select = document.getElementById('dkLanguageSelect');
        const fromSelect = select && select.value;
        const fromStorage = localStorage.getItem('versa-dukhach-lang');
        const fromUrl = new URL(window.location.href).searchParams.get('lang');
        return fromSelect || fromUrl || fromStorage || 'vi';
    }

    function addMessage(text, type) {
        const item = document.createElement('div');
        item.className = 'dk-ai-message ' + type;

        const bubble = document.createElement('div');
        bubble.className = 'dk-ai-bubble';
        bubble.textContent = text;

        item.appendChild(bubble);
        messages.appendChild(item);
        messages.scrollTop = messages.scrollHeight;
        return item;
    }

    function setBusy(isBusy) {
        input.disabled = isBusy;
        form.querySelector('button').disabled = isBusy;
    }

    async function askAi(question) {
        const clean = (question || '').trim();
        if (!clean) return;

        root.classList.add('open');
        addMessage(clean, 'user');
        input.value = '';

        const loading = addMessage('AI đang trả lời...', 'bot loading');
        setBusy(true);

        try {
            const response = await fetch('/DuKhach/AiChat/Ask', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                credentials: 'same-origin',
                body: JSON.stringify({
                    message: clean,
                    languageCode: getCurrentLanguage(),
                    currentPath: window.location.pathname + window.location.search
                })
            });

            let data = null;
            try {
                data = await response.json();
            } catch (_) {
                data = null;
            }

            loading.remove();

            if (!response.ok) {
                addMessage(data?.reply || 'Chatbox AI đang lỗi. Bạn thử lại sau nhé.', 'bot');
                return;
            }

            addMessage(data?.reply || 'Mình chưa có câu trả lời phù hợp.', 'bot');
        } catch (error) {
            loading.remove();
            addMessage('Không kết nối được AI chatbox. Kiểm tra server hoặc Gemini API key rồi thử lại nhé.', 'bot');
        } finally {
            setBusy(false);
            input.focus();
        }
    }

    toggle.addEventListener('click', function () {
        root.classList.toggle('open');
        if (root.classList.contains('open')) {
            setTimeout(() => input.focus(), 120);
        }
    });

    close?.addEventListener('click', function () {
        root.classList.remove('open');
    });

    form.addEventListener('submit', function (event) {
        event.preventDefault();
        askAi(input.value);
    });

    document.querySelectorAll('[data-ai-suggest]').forEach(button => {
        button.addEventListener('click', function () {
            askAi(this.getAttribute('data-ai-suggest'));
        });
    });
})();
