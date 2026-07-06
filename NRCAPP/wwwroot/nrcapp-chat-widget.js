(function () {
    var meta = document.querySelector('meta[name="nrcapp-ai-api"]');
    var API_URL = meta ? meta.getAttribute('content') : 'http://localhost:8000';
    var PANEL_WIDTH = 420;

    var style = document.createElement('style');
    style.textContent = `
.nrcapp-chat-bubble {
    position: fixed;
    bottom: 24px;
    left: 24px;
    width: 56px;
    height: 56px;
    border-radius: 50%;
    background: linear-gradient(135deg, #087F8C, #0F3D4A);
    color: #fff;
    border: none;
    cursor: pointer;
    box-shadow: 0 4px 20px rgba(8,127,140,0.35);
    z-index: 9999;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 26px;
    transition: transform 0.2s, box-shadow 0.2s;
}
.nrcapp-chat-bubble:hover {
    transform: scale(1.08);
    box-shadow: 0 6px 28px rgba(8,127,140,0.45);
}
.nrcapp-chat-bubble svg {
    width: 28px;
    height: 28px;
    fill: currentColor;
}

.nrcapp-chat-panel {
    position: fixed;
    top: 0;
    left: 0;
    width: 420px;
    max-width: 100vw;
    height: 100vh;
    max-height: 100vh;
    background: #fff;
    border-radius: 0;
    box-shadow: 4px 0 24px rgba(0,0,0,0.15);
    z-index: 9998;
    display: none;
    flex-direction: column;
    overflow: hidden;
    direction: rtl;
    font-family: 'Cairo', 'Noto Sans Arabic', system-ui, sans-serif;
}
.nrcapp-chat-panel.open {
    display: flex;
}

.nrcapp-chat-header {
    background: linear-gradient(135deg, #087F8C, #0F3D4A);
    color: #fff;
    padding: 16px 20px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
}
.nrcapp-chat-header h3 {
    margin: 0;
    font-size: 16px;
    font-weight: 950;
}
.nrcapp-chat-header span {
    font-size: 12px;
    opacity: 0.85;
    font-weight: 700;
}
.nrcapp-chat-close {
    background: none;
    border: none;
    color: #fff;
    cursor: pointer;
    font-size: 22px;
    padding: 0;
    opacity: 0.8;
}
.nrcapp-chat-close:hover { opacity: 1; }

.nrcapp-chat-messages {
    flex: 1;
    overflow-y: auto;
    padding: 16px;
    display: flex;
    flex-direction: column;
    gap: 12px;
    background: #F4F7FB;
}

.nrcapp-chat-msg {
    max-width: 85%;
    padding: 10px 14px;
    border-radius: 12px;
    font-size: 14px;
    line-height: 1.7;
    font-weight: 600;
    word-wrap: break-word;
}
.nrcapp-chat-msg.user {
    align-self: flex-end;
    background: linear-gradient(135deg, #087F8C, #0F3D4A);
    color: #fff;
    border-bottom-left-radius: 4px;
}
.nrcapp-chat-msg.bot {
    align-self: flex-start;
    background: #fff;
    color: #17212B;
    border: 1px solid rgba(23,33,43,0.1);
    border-bottom-right-radius: 4px;
}

.nrcapp-chat-msg.bot .sources {
    margin-top: 8px;
    padding-top: 8px;
    border-top: 1px solid rgba(23,33,43,0.1);
    font-size: 12px;
    font-weight: 700;
    color: #64748B;
}
.nrcapp-chat-msg.bot .sources a {
    display: inline-block;
    margin: 2px 0 2px 8px;
    color: #087F8C;
    text-decoration: none;
    font-weight: 800;
}
.nrcapp-chat-msg.bot .sources a:hover {
    text-decoration: underline;
}

.nrcapp-chat-msg.typing {
    align-self: flex-start;
    background: #fff;
    border: 1px solid rgba(23,33,43,0.1);
    border-bottom-right-radius: 4px;
    display: flex;
    gap: 4px;
    padding: 14px 18px;
}
.nrcapp-chat-msg.typing span {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: #087F8C;
    animation: nrcapp-bounce 1.2s infinite;
}
.nrcapp-chat-msg.typing span:nth-child(2) { animation-delay: 0.2s; }
.nrcapp-chat-msg.typing span:nth-child(3) { animation-delay: 0.4s; }
@keyframes nrcapp-bounce {
    0%, 60%, 100% { transform: translateY(0); opacity: 0.4; }
    30% { transform: translateY(-6px); opacity: 1; }
}

.nrcapp-chat-msg.bot.error {
    background: #FEF2F2;
    border-color: #FECACA;
    color: #991B1B;
}

.nrcapp-chat-input-area {
    display: flex;
    gap: 8px;
    padding: 12px 16px;
    border-top: 1px solid rgba(23,33,43,0.1);
    background: #fff;
}
.nrcapp-chat-input-area input {
    flex: 1;
    border: 1px solid rgba(23,33,43,0.15);
    border-radius: 10px;
    padding: 10px 14px;
    font-size: 14px;
    font-family: inherit;
    font-weight: 600;
    outline: none;
    transition: border-color 0.2s;
}
.nrcapp-chat-input-area input:focus {
    border-color: #087F8C;
}
.nrcapp-chat-input-area button {
    background: linear-gradient(135deg, #087F8C, #0F3D4A);
    color: #fff;
    border: none;
    border-radius: 10px;
    width: 44px;
    height: 44px;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 18px;
    transition: opacity 0.2s;
    flex-shrink: 0;
}
.nrcapp-chat-input-area button:hover { opacity: 0.9; }
.nrcapp-chat-input-area button:disabled { opacity: 0.5; cursor: default; }

body.nrcapp-chat-open .app-shell,
body.nrcapp-chat-open .entry-main,
body.nrcapp-chat-open #blazor-error-ui {
    margin-left: 420px;
    transition: margin-left 0.3s ease;
}
.app-shell,
.entry-main {
    transition: margin-left 0.3s ease;
}

@media (max-width: 860px) {
    .nrcapp-chat-panel {
        left: 0;
        right: 0;
        top: 0;
        width: 100%;
        height: 100vh;
        max-width: none;
        box-shadow: none;
    }
    body.nrcapp-chat-open .app-shell,
    body.nrcapp-chat-open .entry-main,
    body.nrcapp-chat-open #blazor-error-ui {
        margin-left: 0;
    }
}
    `;
    document.head.appendChild(style);

    var chatHTML = `
    <button class="nrcapp-chat-bubble" id="nrcappChatBubble" aria-label="فتح المساعد">
        <svg viewBox="0 0 24 24"><path d="M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm0 14H5.17L4 17.17V4h16v12z"/><path d="M7 9h10v2H7zm0-3h7v2H7z"/></svg>
    </button>
    <div class="nrcapp-chat-panel" id="nrcappChatPanel">
        <div class="nrcapp-chat-header">
            <div>
                <h3>مساعد نقطة</h3>
                <span>مركز تنسيق الإغاثة</span>
            </div>
            <button class="nrcapp-chat-close" id="nrcappChatClose" aria-label="إغلاق">✕</button>
        </div>
        <div class="nrcapp-chat-messages" id="nrcappChatMessages">
            <div class="nrcapp-chat-msg bot">
                مرحباً! أنا مساعد موقع نقطة. يمكنني مساعدتك في الاستفسار عن الخدمات، التسجيل، التوزيعات، والمزيد. كيف يمكنني مساعدتك؟
            </div>
        </div>
        <div class="nrcapp-chat-input-area">
            <input type="text" id="nrcappChatInput" placeholder="اكتب سؤالك هنا..." />
            <button id="nrcappChatSend" aria-label="إرسال">
                <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor"><path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/></svg>
            </button>
        </div>
    </div>`;

    var div = document.createElement('div');
    div.innerHTML = chatHTML;
    document.body.appendChild(div);

    var bubble = document.getElementById('nrcappChatBubble');
    var panel = document.getElementById('nrcappChatPanel');
    var closeBtn = document.getElementById('nrcappChatClose');
    var messages = document.getElementById('nrcappChatMessages');
    var input = document.getElementById('nrcappChatInput');
    var sendBtn = document.getElementById('nrcappChatSend');

    function shiftSite(shift) {
        if (shift) {
            document.body.classList.add('nrcapp-chat-open');
        } else {
            document.body.classList.remove('nrcapp-chat-open');
        }
    }

    function togglePanel(open) {
        panel.classList.toggle('open', open);
        bubble.style.display = open ? 'none' : 'flex';
        shiftSite(open);
        if (open) {
            input.focus();
            setTimeout(function () { messages.scrollTop = messages.scrollHeight; }, 100);
        }
    }

    bubble.addEventListener('click', function () { togglePanel(true); });
    closeBtn.addEventListener('click', function () { togglePanel(false); });

    function addMessage(text, role, sources) {
        var div = document.createElement('div');
        div.className = 'nrcapp-chat-msg ' + role;

        if (role === 'bot' && sources && sources.length) {
            div.innerHTML = '<div>' + text + '</div><div class="sources">المصادر: ' +
                sources.map(function (s) { return '<a href="' + s.url + '" target="_self">' + s.title + '</a>'; }).join('') +
                '</div>';
        } else {
            div.textContent = text;
        }
        messages.appendChild(div);
        messages.scrollTop = messages.scrollHeight;
    }

    function showTyping() {
        var div = document.createElement('div');
        div.className = 'nrcapp-chat-msg typing';
        div.id = 'nrcappTypingIndicator';
        div.innerHTML = '<span></span><span></span><span></span>';
        messages.appendChild(div);
        messages.scrollTop = messages.scrollHeight;
    }

    function hideTyping() {
        var el = document.getElementById('nrcappTypingIndicator');
        if (el) el.remove();
    }

    function sendQuestion() {
        var question = input.value.trim();
        if (!question) return;

        addMessage(question, 'user');
        input.value = '';
        sendBtn.disabled = true;
        showTyping();

        fetch(API_URL + '/ask', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ question: question })
        })
        .then(function (r) {
            if (!r.ok) throw new Error('خطأ في الاتصال');
            return r.json();
        })
        .then(function (data) {
            hideTyping();
            addMessage(data.answer, 'bot', data.sources || []);
        })
        .catch(function (err) {
            hideTyping();
            addMessage('عذراً، حدث خطأ في الاتصال بالمساعد. يرجى المحاولة لاحقاً.', 'bot error');
        })
        .finally(function () {
            sendBtn.disabled = false;
        });
    }

    sendBtn.addEventListener('click', sendQuestion);
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') sendQuestion();
    });
})();
