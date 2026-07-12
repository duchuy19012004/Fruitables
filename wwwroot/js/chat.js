/**
 * Fruitables storefront chat client.
 * Initializes every element with [data-chat-root] once (page | widget).
 */
(function () {
    'use strict';

    if (window.__fruitablesChatInitialized) {
        return;
    }
    window.__fruitablesChatInitialized = true;

    var SESSION_KEY_PREFIX = 'fruitables_chat_session_';

    function qs(root, sel) {
        return root.querySelector(sel);
    }

    function escapeHtml(text) {
        var div = document.createElement('div');
        div.textContent = text == null ? '' : String(text);
        return div.innerHTML;
    }

    function showToast(message, isWarning) {
        if (typeof window.showRealtimeToast === 'function') {
            window.showRealtimeToast(escapeHtml(message), !!isWarning);
            return;
        }
        // Fallback: brief alert-free console for environments without toast helper
        console.warn('[chat]', message);
    }

    function setError(root, message) {
        var el = qs(root, '[data-chat-error]');
        if (!el) return;
        if (!message) {
            el.classList.add('d-none');
            el.textContent = '';
            return;
        }
        el.textContent = message;
        el.classList.remove('d-none');
    }

    function scrollMessages(root) {
        var list = qs(root, '[data-chat-messages]');
        if (list) {
            list.scrollTop = list.scrollHeight;
        }
    }

    function appendBubble(root, role, contentHtml, extraClass) {
        var list = qs(root, '[data-chat-messages]');
        if (!list) return null;

        // Remove welcome on first real message
        var welcome = list.querySelector('.chat-welcome');
        if (welcome && (role === 'user' || role === 'assistant')) {
            welcome.remove();
        }

        var bubble = document.createElement('div');
        bubble.className =
            'chat-bubble chat-bubble-' +
            (role === 'user' ? 'user' : 'assistant') +
            (extraClass ? ' ' + extraClass : '');
        bubble.innerHTML = contentHtml;
        list.appendChild(bubble);
        scrollMessages(root);
        return bubble;
    }

    function renderMessage(root, msg) {
        var role = (msg.role || msg.Role || 'assistant').toLowerCase();
        var content = msg.content || msg.Content || '';
        var refused = !!(msg.refused || msg.Refused);

        var html = escapeHtml(content);
        if (refused && role === 'assistant') {
            html +=
                '<span class="chat-refused-note">' +
                'Bạn có thể <a href="/Contact">liên hệ hỗ trợ</a> để được giúp thêm.' +
                '</span>';
        }
        appendBubble(root, role === 'user' ? 'user' : 'assistant', html);
    }

    function setBusy(root, busy) {
        var input = qs(root, '[data-chat-input]');
        var send = qs(root, '[data-chat-send]');
        var chips = root.querySelectorAll('[data-chat-chip]');
        if (input) input.disabled = busy;
        if (send) send.disabled = busy;
        chips.forEach(function (c) {
            c.disabled = busy;
        });
        root.dataset.chatBusy = busy ? '1' : '0';
    }

    function storageKey(source) {
        return SESSION_KEY_PREFIX + (source || 'widget');
    }

    function getStoredSessionId(source) {
        try {
            return sessionStorage.getItem(storageKey(source)) || null;
        } catch (e) {
            return null;
        }
    }

    function setStoredSessionId(source, id) {
        try {
            if (id) sessionStorage.setItem(storageKey(source), id);
        } catch (e) {
            /* ignore */
        }
    }

    async function api(url, options) {
        var opts = options || {};
        opts.credentials = 'same-origin';
        opts.headers = opts.headers || {};
        if (opts.body && !opts.headers['Content-Type']) {
            opts.headers['Content-Type'] = 'application/json';
        }
        var res = await fetch(url, opts);
        var data = null;
        var text = await res.text();
        if (text) {
            try {
                data = JSON.parse(text);
            } catch (e) {
                data = { error: text };
            }
        }
        return { ok: res.ok, status: res.status, data: data };
    }

    function errorMessageFrom(status, data) {
        var msg =
            (data && (data.error || data.message || data.Message || data.Error)) ||
            null;
        if (status === 429) {
            return msg || 'Bạn gửi quá nhanh. Vui lòng thử lại sau.';
        }
        if (status === 400) {
            return msg || 'Tin nhắn không hợp lệ. Vui lòng kiểm tra lại.';
        }
        if (status === 503) {
            return msg || 'Hệ thống tạm thời không khả dụng. Vui lòng thử lại sau.';
        }
        return msg || 'Đã xảy ra lỗi. Vui lòng thử lại.';
    }

    async function ensureSession(root) {
        if (root._chatSessionId) {
            return root._chatSessionId;
        }

        var source = root.getAttribute('data-chat-root') || 'widget';
        var stored = getStoredSessionId(source);
        if (stored) {
            root._chatSessionId = stored;
            return stored;
        }

        var result = await api('/api/chat/sessions', {
            method: 'POST',
            body: JSON.stringify({ source: source })
        });

        if (!result.ok) {
            var err = errorMessageFrom(result.status, result.data);
            setError(root, err);
            if (result.status === 429 || result.status === 503 || result.status === 400) {
                showToast(err, true);
            }
            throw new Error(err);
        }

        var sessionId =
            (result.data && (result.data.sessionId || result.data.SessionId)) || null;
        if (!sessionId) {
            throw new Error('Không nhận được session.');
        }

        root._chatSessionId = sessionId;
        setStoredSessionId(source, sessionId);
        return sessionId;
    }

    async function loadHistory(root, sessionId) {
        if (!sessionId) return;
        var result = await api(
            '/api/chat/sessions/' + encodeURIComponent(sessionId) + '/messages',
            { method: 'GET' }
        );

        if (result.status === 403) {
            // Cookie/session mismatch — start fresh next send
            root._chatSessionId = null;
            try {
                sessionStorage.removeItem(storageKey(root.getAttribute('data-chat-root')));
            } catch (e) { /* ignore */ }
            return;
        }

        if (!result.ok) {
            // History is optional; don't block UI
            return;
        }

        var messages = Array.isArray(result.data) ? result.data : [];
        if (!messages.length) return;

        var list = qs(root, '[data-chat-messages]');
        if (list) {
            list.innerHTML = '';
        }
        messages.forEach(function (m) {
            renderMessage(root, m);
        });
        scrollMessages(root);
    }

    async function sendMessage(root, text) {
        var message = (text || '').trim();
        if (!message) return;
        if (root.dataset.chatBusy === '1') return;

        setError(root, null);
        setBusy(root, true);

        var input = qs(root, '[data-chat-input]');
        if (input) input.value = '';

        appendBubble(root, 'user', escapeHtml(message));
        var typing = appendBubble(root, 'assistant', 'Đang soạn trả lời…', 'chat-bubble-typing');

        var source = root.getAttribute('data-chat-root') || 'widget';

        try {
            var sessionId = await ensureSession(root);
            var result = await api('/api/chat/messages', {
                method: 'POST',
                body: JSON.stringify({
                    message: message,
                    source: source,
                    sessionId: sessionId
                })
            });

            if (typing && typing.parentNode) typing.remove();

            if (!result.ok) {
                var err = errorMessageFrom(result.status, result.data);
                setError(root, err);
                showToast(err, true);
                return;
            }

            var data = result.data || {};
            var newSession =
                data.sessionId || data.SessionId || sessionId;
            if (newSession) {
                root._chatSessionId = newSession;
                setStoredSessionId(source, newSession);
            }

            var assistant =
                data.assistantMessage || data.AssistantMessage || null;
            if (assistant) {
                renderMessage(root, assistant);
            } else if (data.content || data.Content) {
                renderMessage(root, data);
            }
        } catch (e) {
            if (typing && typing.parentNode) typing.remove();
            var fallback = (e && e.message) || 'Không thể gửi tin nhắn.';
            setError(root, fallback);
            showToast(fallback, true);
        } finally {
            setBusy(root, false);
            if (input) input.focus();
        }
    }

    function bindRoot(root) {
        if (root.dataset.chatBound === '1') return;
        root.dataset.chatBound = '1';

        var form = qs(root, '[data-chat-form]');
        var input = qs(root, '[data-chat-input]');

        if (form) {
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                sendMessage(root, input ? input.value : '');
            });
        }

        root.querySelectorAll('[data-chat-chip]').forEach(function (chip) {
            chip.addEventListener('click', function () {
                var text =
                    chip.getAttribute('data-chat-chip') || chip.textContent || '';
                if (input) input.value = text;
                sendMessage(root, text);
            });
        });
    }

    async function initRoot(root) {
        bindRoot(root);
        try {
            var sessionId = await ensureSession(root);
            await loadHistory(root, sessionId);
        } catch (e) {
            // ensureSession already surfaced the error
        }
    }

    function initWidgetToggle() {
        var widget = document.getElementById('chatWidget');
        var toggle = document.getElementById('chatWidgetToggle');
        var panel = document.getElementById('chatWidgetPanel');
        if (!widget || !toggle || !panel) return;

        toggle.addEventListener('click', function () {
            var isOpen = widget.classList.toggle('is-open');
            if (isOpen) {
                panel.hidden = false;
                toggle.setAttribute('aria-expanded', 'true');
                toggle.setAttribute('aria-label', 'Đóng chat hỗ trợ');
                var input = panel.querySelector('[data-chat-input]');
                if (input) setTimeout(function () { input.focus(); }, 50);
            } else {
                panel.hidden = true;
                toggle.setAttribute('aria-expanded', 'false');
                toggle.setAttribute('aria-label', 'Mở chat hỗ trợ');
            }
        });
    }

    function boot() {
        initWidgetToggle();
        document.querySelectorAll('[data-chat-root]').forEach(function (root) {
            initRoot(root);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})();
