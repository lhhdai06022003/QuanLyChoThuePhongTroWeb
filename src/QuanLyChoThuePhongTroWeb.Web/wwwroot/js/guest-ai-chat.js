(function () {
    'use strict';

    const money = value => typeof value === 'number' && Number.isFinite(value)
        ? new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value)
        : 'Chưa cập nhật';
    function node(doc, tag, className, text) {
        const el = doc.createElement(tag);
        if (className) el.className = className;
        if (text != null) el.textContent = String(text);
        return el;
    }
    function createRoomCard(room, doc) {
        if (!Number.isSafeInteger(room?.id) || room.id <= 0) return null;
        const card = node(doc, 'article', 'guest-ai-room');
        const title = node(doc, 'h4');
        const detail = node(doc, 'a', '', room.title || 'Phòng ' + room.number);
        detail.href = '/phong/' + room.id;
        title.append(detail);
        const facts = [room.branch, room.area > 0 ? room.area + ' m²' : null, room.occupants > 0 ? room.occupants + ' người' : null].filter(Boolean);
        card.append(title, node(doc, 'p', '', facts.join(' · ')));
        const rent = node(doc, 'div', 'guest-ai-room-cost');
        rent.append(node(doc, 'span', '', 'Thuê / tháng'), node(doc, 'strong', '', money(room.rent)));
        const deposit = node(doc, 'div', 'guest-ai-room-cost guest-ai-room-deposit');
        const canBook = typeof room.depositAmount === 'number' && Number.isFinite(room.depositAmount) && room.depositAmount > 0;
        if (canBook) deposit.append(node(doc, 'span', '', 'Cọc giữ chỗ'), node(doc, 'strong', '', money(room.depositAmount)));
        else deposit.append(node(doc, 'span', '', 'Tiền cọc'), node(doc, 'span', 'guest-ai-room-unavailable', 'Chưa mở đặt cọc'));
        const actions = node(doc, 'div', 'guest-ai-room-actions');
        const viewing = node(doc, 'a', '', 'Hẹn xem');
        viewing.href = '/phong/' + room.id + '/hen-xem';
        actions.append(viewing);
        if (canBook) {
            const booking = node(doc, 'a', '', 'Đặt phòng');
            booking.href = '/phong/' + room.id + '/giu-cho';
            actions.append(booking);
        }
        const prices = node(doc, 'div', 'guest-ai-room-prices');
        prices.append(rent, deposit);
        card.append(prices, actions);
        return card;
    }

    function createAnswer(answer, doc) {
        const text = String(answer || '').trim();
        if (text.length <= 240 && text.split('\n').length <= 4)
            return node(doc, 'div', 'guest-ai-bubble', text);
        const details = node(doc, 'details', 'guest-ai-answer');
        const summary = node(doc, 'summary');
        const preview = text.replace(/\s+/g, ' ').slice(0, 180).replace(/\s+\S*$/, '') + '…';
        summary.append(node(doc, 'span', 'guest-ai-answer-preview', preview),
            node(doc, 'span', 'guest-ai-answer-more', 'Đọc đầy đủ'),
            node(doc, 'span', 'guest-ai-answer-less', 'Thu gọn'));
        details.append(summary, node(doc, 'div', 'guest-ai-answer-full', text));
        return details;
    }

    function buildPayload(message, history, roomId, suggestedRoomIds = []) {
        const payload = {
            message: String(message).trim(),
            roomId: Number.isSafeInteger(roomId) && roomId > 0 ? roomId : null,
            suggestedRoomIds: (Array.isArray(suggestedRoomIds) ? suggestedRoomIds : [])
                .filter(id => Number.isSafeInteger(id) && id > 0).slice(0, 4),
            history: (Array.isArray(history) ? history : []).filter(h => h &&
                ['user', 'model'].includes(h.role) && typeof h.message === 'string' && h.message.trim())
                .slice(-8).map(h => ({ role: h.role, message: h.message.slice(0, h.role === 'user' ? 1000 : 3000) }))
        };
        // Keep UTF-8 bodies below the API limit even when Vietnamese history is long.
        while (payload.history.length && new TextEncoder().encode(JSON.stringify(payload)).length > 30000)
            payload.history.shift();
        return payload;
    }

    function init(root) {
        const doc = root.ownerDocument;
        const panel = root.querySelector('#guest-ai-panel');
        const opener = root.querySelector('[data-ai-open]');
        const input = root.querySelector('[data-ai-input]');
        const form = root.querySelector('[data-ai-form]');
        const messages = root.querySelector('[data-ai-messages]');
        const thinking = root.querySelector('[data-ai-thinking]');
        const welcome = root.querySelector('[data-ai-welcome]');
        const conversation = root.querySelector('[data-ai-conversation]');
        const submit = root.querySelector('[data-ai-send]');
        const roomId = Number(root.dataset.roomId);
        let history = [], suggestedRoomIds = [], busy = false, generation = 0, pending = null;
        const scroll = () => { conversation.scrollTop = conversation.scrollHeight; };
        const toggle = open => {
            panel.hidden = !open;
            opener.setAttribute('aria-expanded', String(open));
            if (open) { if (!busy) input.focus(); }
            else opener.focus();
        };
        const setBusy = value => {
            busy = value;
            thinking.hidden = !value;
            input.disabled = value;
            submit.disabled = value;
            form.setAttribute('aria-busy', String(value));
        };
        async function send(text, isRetry) {
            const message = text.trim();
            if (busy || !message || message.length > 1000) return;
            const current = generation;
            welcome.hidden = true;
            root.dataset.started = 'true';
            if (!isRetry) messages.append(node(doc, 'div', 'guest-ai-bubble guest-ai-bubble-user', message));
            input.value = '';
            setBusy(true);
            scroll();
            const controller = new AbortController();
            pending = controller;
            const timeout = setTimeout(() => controller.abort(), 45000);
            let errorContainer;
            try {
                const response = await fetch(root.dataset.endpoint, {
                    method: 'POST', credentials: 'same-origin', signal: controller.signal,
                    headers: { 'Content-Type': 'application/json',
                        RequestVerificationToken: form.querySelector('[name="__RequestVerificationToken"]').value },
                    body: JSON.stringify(buildPayload(message, history, roomId, suggestedRoomIds))
                });
                const data = await response.json().catch(() => null);
                if (current !== generation) return;
                if (!response.ok || !data?.success) {
                    throw new Error(data?.message || (response.status === 400
                        ? 'Phiên gửi câu hỏi đã hết hạn. Hãy tải lại trang rồi thử lại.'
                        : 'Chưa kết nối được trợ lý AI. Bạn thử lại hoặc dùng bộ lọc tìm phòng nhé.'));
                }
                const reply = node(doc, 'div', 'guest-ai-reply');
                const answer = String(data.message || '').slice(0, 4000);
                reply.append(createAnswer(answer, doc));
                const nextIds = [];
                for (const room of (Array.isArray(data.rooms) ? data.rooms : []).slice(0, 4)) {
                    const card = createRoomCard(room, doc);
                    if (card) { reply.append(card); nextIds.push(room.id); }
                }
                if (nextIds.length) suggestedRoomIds = nextIds;
                messages.append(reply);
                history.push({ role: 'user', message }, { role: 'model', message: answer });
                history = history.slice(-8);
            } catch (error) {
                if (current !== generation) return;
                input.value = message;
                errorContainer = node(doc, 'div', 'guest-ai-bubble guest-ai-error', error.name === 'AbortError'
                    ? 'Chưa nhận được phản hồi. Bạn thử lại hoặc dùng bộ lọc tìm phòng nhé.'
                    : String(error.message || 'Kết nối bị gián đoạn. Bạn hãy thử lại.').slice(0, 1000));
                const retry = node(doc, 'button', 'guest-ai-retry', 'Thử lại');
                retry.type = 'button';
                retry.addEventListener('click', () => {
                    if (busy) return;
                    errorContainer.remove();
                    void send(message, true);
                });
                errorContainer.append(retry);
                messages.append(errorContainer);
            } finally {
                clearTimeout(timeout);
                if (current === generation) { pending = null; setBusy(false); scroll(); }
            }
        }
        opener.addEventListener('click', () => toggle(panel.hidden));
        root.querySelector('[data-ai-close]').addEventListener('click', () => toggle(false));
        doc.addEventListener('keydown', event => {
            if (event.key === 'Escape' && !panel.hidden) { event.preventDefault(); toggle(false); }
        });
        root.querySelector('[data-ai-reset]').addEventListener('click', () => {
            generation++;
            pending?.abort();
            pending = null;
            history = [];
            suggestedRoomIds = [];
            messages.replaceChildren();
            input.value = '';
            welcome.hidden = false;
            root.dataset.started = 'false';
            setBusy(false);
            input.focus();
        });
        for (const button of root.querySelectorAll('[data-ai-suggestion]'))
            button.addEventListener('click', () => { void send(button.dataset.aiSuggestion); });
        form.addEventListener('submit', event => { event.preventDefault(); void send(input.value); });
        input.addEventListener('keydown', event => {
            if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
                event.preventDefault();
                form.requestSubmit();
            }
        });
        window.addEventListener('pagehide', () => pending?.abort());
        root.hidden = false;
    }

    if (typeof module !== 'undefined' && module.exports) module.exports = { createRoomCard, createAnswer, buildPayload, init };
    if (typeof document !== 'undefined') document.addEventListener('DOMContentLoaded', () => {
        const root = document.querySelector('[data-guest-ai]');
        if (root) init(root);
    });
})();
