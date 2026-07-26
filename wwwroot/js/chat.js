// ============================================================
// GIAO DIỆN CHAT TRÊN TRÌNH DUYỆT (widget + trang /Chat)
//
// Việc file này làm (không lộ API key AI):
// 1) Mở / nhớ cuộc chat (cookie + sessionStorage)
// 2) Gửi tin lên /api/chat/messages/stream (SSE token khi AI generate)
// 3) Vẽ bong bóng tin nhắn (cập nhật dần khi stream)
// 4) Hiện lỗi thân thiện (quá nhanh, hệ thống bận...)
// ============================================================
(function () {
    'use strict';

    // Chạy 1 lần thôi, tránh gắn sự kiện 2 lần
    if (window.__fruitablesChatInitialized) {
        return;
    }
    window.__fruitablesChatInitialized = true;

    // Key lưu session id trên trình duyệt (bổ sung cho cookie)
    var SESSION_KEY_PREFIX = 'fruitables_chat_session_';

    // Tìm 1 phần tử con trong khung chat
    function qs(root, sel) {
        return root.querySelector(sel);
    }

    // Chống XSS: đưa chữ thuần vào HTML an toàn
    function escapeHtml(text) {
        var div = document.createElement('div');
        div.textContent = text == null ? '' : String(text);
        return div.innerHTML;
    }

    // Toast góc màn hình (nếu site có sẵn), không thì log console
    function showToast(message, isWarning) {
        if (typeof window.showRealtimeToast === 'function') {
            window.showRealtimeToast(escapeHtml(message), !!isWarning);
            return;
        }
        console.warn('[chat]', message);
    }

    // Hiện / ẩn dòng lỗi trong khung chat
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

    // Cuộn xuống tin mới nhất
    function scrollMessages(root) {
        var list = qs(root, '[data-chat-messages]');
        if (list) {
            list.scrollTop = list.scrollHeight;
        }
    }

    // Thêm 1 bong bóng tin
    function appendBubble(root, role, contentHtml, extraClass) {
        var list = qs(root, '[data-chat-messages]');
        if (!list) return null;

        // Có tin thật → bỏ dòng chào mừng
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

    // Cập nhật nội dung bong bóng assistant đang stream (plain text → escaped)
    function updateStreamingBubble(bubble, text, isStreaming) {
        if (!bubble) return;
        var html = escapeHtml(text || '');
        if (isStreaming) {
            html += '<span class="chat-stream-cursor" aria-hidden="true"></span>';
            bubble.classList.add('chat-bubble-streaming');
            bubble.classList.remove('chat-bubble-typing');
        } else {
            bubble.classList.remove('chat-bubble-streaming');
        }
        bubble.innerHTML = html;
        var list = bubble.parentElement;
        if (list) {
            list.scrollTop = list.scrollHeight;
        }
    }

    function finalizeAssistantBubble(bubble, text, refused) {
        if (!bubble) return;
        var html = escapeHtml(text || '');
        // RagService đã có hướng dẫn liên hệ trong RefuseMessage, không thêm nữa
        bubble.classList.remove('chat-bubble-streaming', 'chat-bubble-typing');
        bubble.innerHTML = html;
    }

    // Vẽ 1 tin từ API
    function renderMessage(root, msg) {
        var role = (msg.role || msg.Role || 'assistant').toLowerCase();
        var content = msg.content || msg.Content || '';

        var html = escapeHtml(content);
        // RagService đã có hướng dẫn liên hệ trong RefuseMessage, không thêm nữa
        appendBubble(root, role === 'user' ? 'user' : 'assistant', html);
    }

    // Khóa ô nhập khi đang chờ bot
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
            /* trình duyệt chặn storage thì bỏ qua */
        }
    }

    // Gọi API nội bộ (kèm cookie)
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

    // Đổi mã lỗi HTTP → câu tiếng Việt dễ hiểu
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

    // Parse buffer SSE → mảng { event, data }
    function parseSseChunk(buffer) {
        var events = [];
        // SSE blocks ngăn bởi \n\n
        var parts = buffer.split('\n\n');
        // Phần cuối có thể chưa đủ block
        var rest = parts.pop() || '';
        for (var i = 0; i < parts.length; i++) {
            var block = parts[i];
            if (!block || !block.trim()) continue;
            var eventName = 'message';
            var dataLines = [];
            var lines = block.split('\n');
            for (var j = 0; j < lines.length; j++) {
                var line = lines[j];
                if (line.indexOf('event:') === 0) {
                    eventName = line.slice(6).trim();
                } else if (line.indexOf('data:') === 0) {
                    dataLines.push(line.slice(5).trim());
                }
            }
            if (!dataLines.length) continue;
            var raw = dataLines.join('\n');
            var data = null;
            try {
                data = JSON.parse(raw);
            } catch (e) {
                data = { text: raw };
            }
            events.push({ event: eventName, data: data });
        }
        return { events: events, rest: rest };
    }

    // Gửi tin + đọc SSE token
    async function streamChat(root, sessionId, message, source, onEvent) {
        var res = await fetch('/api/chat/messages/stream', {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                Accept: 'text/event-stream'
            },
            body: JSON.stringify({
                message: message,
                source: source,
                sessionId: sessionId
            })
        });

        var contentType = (res.headers.get('content-type') || '').toLowerCase();
        var isSse = contentType.indexOf('text/event-stream') !== -1;

        // Lỗi sớm (400/429/503) có thể là JSON thay vì SSE
        if (!res.ok && !isSse) {
            var errData = null;
            var errText = await res.text();
            try {
                errData = errText ? JSON.parse(errText) : null;
            } catch (e) {
                errData = { error: errText };
            }
            var err = new Error(errorMessageFrom(res.status, errData));
            err.status = res.status;
            throw err;
        }

        if (!res.body || !res.body.getReader) {
            throw new Error('Trình duyệt không hỗ trợ streaming.');
        }

        var reader = res.body.getReader();
        var decoder = new TextDecoder('utf-8');
        var buffer = '';
        var sawDone = false;

        while (true) {
            var chunk = await reader.read();
            if (chunk.done) break;
            buffer += decoder.decode(chunk.value, { stream: true });
            var parsed = parseSseChunk(buffer);
            buffer = parsed.rest;
            for (var i = 0; i < parsed.events.length; i++) {
                var ev = parsed.events[i];
                if (ev.event === 'done') sawDone = true;
                onEvent(ev.event, ev.data || {});
            }
        }

        // Flush phần còn lại (nếu server không kết bằng \n\n)
        if (buffer && buffer.trim()) {
            var tail = parseSseChunk(buffer + '\n\n');
            for (var k = 0; k < tail.events.length; k++) {
                var tev = tail.events[k];
                if (tev.event === 'done') sawDone = true;
                onEvent(tev.event, tev.data || {});
            }
        }

        if (!res.ok && !sawDone) {
            var e2 = new Error(errorMessageFrom(res.status, null));
            e2.status = res.status;
            throw e2;
        }

        return { ok: res.ok || sawDone, sawDone: sawDone };
    }

    // Đảm bảo đã có session (tạo mới nếu chưa)
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

    // Tải tin cũ (nếu server cho phép theo cookie)
    async function loadHistory(root, sessionId) {
        if (!sessionId) return;
        var result = await api(
            '/api/chat/sessions/' + encodeURIComponent(sessionId) + '/messages',
            { method: 'GET' }
        );

        if (result.status === 403) {
            // Cookie lệch → xóa id local, lần gửi sau tạo mới
            root._chatSessionId = null;
            try {
                sessionStorage.removeItem(storageKey(root.getAttribute('data-chat-root')));
            } catch (e) { /* ignore */ }
            return;
        }

        if (!result.ok) {
            // Lịch sử không bắt buộc
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

    // Gửi 1 tin của khách (stream token khi AI generate)
    async function sendMessage(root, text) {
        var message = (text || '').trim();
        if (!message) return;
        if (root.dataset.chatBusy === '1') return;

        setError(root, null);
        setBusy(root, true);

        var input = qs(root, '[data-chat-input]');
        if (input) input.value = '';

        appendBubble(root, 'user', escapeHtml(message));
        // Placeholder → sẽ chuyển thành bong bóng stream
        var bubble = appendBubble(
            root,
            'assistant',
            'Đang soạn trả lời…',
            'chat-bubble-typing'
        );

        var source = root.getAttribute('data-chat-root') || 'widget';
        var accumulated = '';
        var streamError = null;
        var finished = false;

        try {
            var sessionId = await ensureSession(root);
            await streamChat(root, sessionId, message, source, function (eventName, data) {
                if (eventName === 'meta') {
                    var sid = data.sessionId || data.SessionId;
                    if (sid) {
                        root._chatSessionId = sid;
                        setStoredSessionId(source, sid);
                    }
                    return;
                }

                if (eventName === 'token') {
                    var delta = data.text || data.Text || '';
                    if (!delta) return;
                    accumulated += delta;
                    updateStreamingBubble(bubble, accumulated, true);
                    return;
                }

                if (eventName === 'done') {
                    finished = true;
                    var finalText =
                        data.text != null
                            ? data.text
                            : data.Text != null
                              ? data.Text
                              : accumulated;
                    var refused = !!(data.refused || data.Refused);
                    var doneSid = data.sessionId || data.SessionId;
                    if (doneSid) {
                        root._chatSessionId = doneSid;
                        setStoredSessionId(source, doneSid);
                    }
                    finalizeAssistantBubble(bubble, finalText, refused);
                    return;
                }

                if (eventName === 'error') {
                    streamError =
                        data.error ||
                        data.Error ||
                        'Hệ thống tạm thời không khả dụng. Vui lòng thử lại sau.';
                }
            });

            if (streamError) {
                if (bubble && bubble.parentNode && !accumulated) {
                    bubble.remove();
                } else if (bubble && accumulated) {
                    finalizeAssistantBubble(bubble, accumulated, false);
                }
                setError(root, streamError);
                showToast(streamError, true);
                return;
            }

            if (!finished && bubble && bubble.parentNode) {
                if (accumulated) {
                    finalizeAssistantBubble(bubble, accumulated, false);
                } else {
                    bubble.remove();
                    setError(root, 'Không nhận được phản hồi từ trợ lý.');
                    showToast('Không nhận được phản hồi từ trợ lý.', true);
                }
            }
        } catch (e) {
            if (bubble && bubble.parentNode && !accumulated) {
                bubble.remove();
            } else if (bubble && accumulated) {
                finalizeAssistantBubble(bubble, accumulated, false);
            }
            var fallback = (e && e.message) || 'Không thể gửi tin nhắn.';
            setError(root, fallback);
            showToast(fallback, true);
        } finally {
            setBusy(root, false);
            if (input) input.focus();
        }
    }

    // Gắn sự kiện submit + nút gợi ý (chip)
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
            // Lỗi đã hiện trên UI
        }
    }

    // Nút tròn góc phải: mở / đóng panel widget
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

    // Khởi động khi trang sẵn sàng
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
