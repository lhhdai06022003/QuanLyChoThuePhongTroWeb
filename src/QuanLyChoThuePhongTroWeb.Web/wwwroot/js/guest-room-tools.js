import { SelectionStore, normalizeIds, restorePendingSelection } from './guest-room-selection.mjs';

let storage;
try { storage = window.localStorage; }
catch { storage = { getItem() { throw new Error('Storage unavailable'); }, setItem() { throw new Error('Storage unavailable'); } }; }
const saved = new SelectionStore(storage, 'system-rental:saved-rooms:v1', 50);
const comparison = new SelectionStore(storage, 'system-rental:compare-rooms:v1', 3);
const savedHost = document.querySelector('[data-saved-rooms]');
const feedback = document.querySelector('[data-room-feedback]');
const money = new Intl.NumberFormat('vi-VN');
let feedbackTimer;
let savedRequest;

function announce(message) {
    if (!feedback) return;
    clearTimeout(feedbackTimer);
    feedback.textContent = message;
    feedback.hidden = false;
    feedbackTimer = setTimeout(() => { feedback.hidden = true; }, 6500);
}

function persistenceNotice() {
    if (!saved.persistent || !comparison.persistent)
        announce('Trình duyệt đang chặn lưu danh sách. Lựa chọn chỉ được giữ trên trang đang mở; so sánh vẫn mở được bằng liên kết.');
}

function syncControls() {
    document.querySelectorAll('[data-save-room]').forEach(button => {
        const selected = saved.ids.includes(Number(button.dataset.saveRoom));
        button.hidden = false;
        button.setAttribute('aria-pressed', String(selected));
        button.setAttribute('aria-label', `${selected ? 'Bỏ lưu' : 'Lưu'} phòng ${button.dataset.roomName || ''}`.trim());
        const label = button.querySelector('[data-save-label]');
        if (label) label.textContent = selected ? 'Đã lưu phòng' : 'Lưu phòng';
    });
    document.querySelectorAll('[data-compare-room]').forEach(input => {
        input.checked = comparison.ids.includes(Number(input.dataset.compareRoom));
        input.closest('label').hidden = false;
    });
    document.querySelectorAll('[data-saved-count]').forEach(badge => {
        badge.textContent = String(saved.ids.length);
        badge.hidden = saved.ids.length === 0;
    });
    document.querySelectorAll('[data-clear-saved]').forEach(button => { button.hidden = saved.ids.length === 0; });
    const bar = document.querySelector('[data-compare-bar]');
    if (bar) {
        bar.hidden = comparison.ids.length === 0;
        bar.querySelector('[data-compare-count]').textContent = String(comparison.ids.length);
        bar.querySelector('[data-compare-hint]').textContent = comparison.ids.length < 2 ? 'Chọn thêm một phòng để đối chiếu' : 'Đối chiếu giá, diện tích và vị trí';
        const link = bar.querySelector('[data-compare-link]');
        link.href = '/phong/so-sanh?ids=' + comparison.ids.join(',');
        link.setAttribute('aria-disabled', String(comparison.ids.length < 2));
    }
    document.body.classList.toggle('room-comparing', comparison.ids.length > 0);
    document.querySelectorAll('[data-copy-page],[data-remove-comparison]').forEach(button => { button.hidden = false; });
    // Keep an in-page selection usable when persistent browser storage is blocked.
    document.querySelectorAll('.room-saved-nav').forEach(link => {
        link.href = !saved.persistent && saved.ids.length ? '/phong/da-luu?ids=' + saved.ids.join(',') : '/phong/da-luu';
    });
}

function node(tag, className, text) {
    const element = document.createElement(tag);
    if (className) element.className = className;
    if (text !== undefined) element.textContent = String(text);
    return element;
}
function link(text, href, className) {
    const anchor = node('a', className, text);
    anchor.href = href;
    return anchor;
}
function icon(name) {
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('class', 'room-icon');
    svg.setAttribute('aria-hidden', 'true');
    const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
    use.setAttribute('href', '#room-' + name);
    svg.append(use);
    return svg;
}
function roomCard(room) {
    const id = Number(room.id);
    const root = '/phong/' + id;
    const card = node('article', 'guest-room-card');
    card.dataset.roomCard = String(id);
    const cover = node('div', 'room-card-cover');
    const visual = link('', root, 'guest-room-visual');
    visual.setAttribute('aria-label', 'Xem chi tiết phòng ' + room.number);
    let imageUrl;
    try {
        const parsed = new URL(room.image, location.origin);
        if (room.image && ['http:', 'https:'].includes(parsed.protocol)) imageUrl = parsed.href;
    } catch { /* A missing image uses the same placeholder as the public list. */ }
    if (imageUrl) {
        const image = node('img'); image.src = imageUrl; image.alt = 'Ảnh phòng ' + room.number; image.loading = 'lazy'; visual.append(image);
    } else {
        const empty = node('span', 'guest-room-no-image');
        empty.append(icon('home'), node('span', '', 'Ảnh phòng đang được cập nhật'));
        visual.append(empty);
    }
    visual.append(node('span', 'guest-room-number', 'Phòng ' + room.number));
    const saveButton = node('button', 'room-save-button'); saveButton.type = 'button'; saveButton.dataset.saveRoom = String(id); saveButton.dataset.roomName = room.number; saveButton.append(icon('heart'));
    cover.append(visual, saveButton);
    const body = node('div', 'guest-room-body');
    const heading = node('h3'); heading.append(link('Phòng ' + room.number, root));
    const address = node('p', 'room-card-address'); address.append(icon('location'), node('span', '', room.address));
    const facts = node('div', 'guest-room-facts');
    const size = node('span'); size.append(icon('area'), document.createTextNode(room.area + ' m²'));
    const people = node('span'); people.append(icon('people'), document.createTextNode('Tối đa ' + room.people + ' người'));
    facts.append(size, people);
    const bottom = node('div', 'guest-room-bottom');
    const price = node('p'); price.append(node('strong', '', money.format(room.price) + ' ₫'), node('span', '', '/ tháng'));
    bottom.append(price, link('Chi tiết', root));
    const choice = node('label', 'room-compare-choice');
    const checkbox = node('input'); checkbox.type = 'checkbox'; checkbox.dataset.compareRoom = String(id); choice.append(checkbox, document.createTextNode('So sánh phòng này'));
    const actions = node('div', 'guest-room-actions');
    actions.append(link('Hẹn xem phòng', root + '/hen-xem', 'guest-button guest-button-outline'), link('Đặt phòng', root + '/giu-cho', 'guest-button guest-button-primary'));
    body.append(node('p', 'room-card-branch', room.branch), heading, address, facts, bottom, choice, actions);
    card.append(cover, body);
    return card;
}

async function loadSavedRooms() {
    if (!savedHost) return;
    savedRequest?.abort();
    const request = new AbortController(); savedRequest = request;
    if (!saved.ids.length) {
        const empty = node('div', 'guest-empty');
        empty.append(icon('heart'), node('h2', '', 'Chưa có phòng nào được lưu'), node('p', '', 'Bấm biểu tượng trái tim trên căn phòng bạn thích để lưu lại ở đây.'), link('Khám phá phòng trống', '/phong', 'guest-button guest-button-primary'));
        savedHost.replaceChildren(empty); savedHost.setAttribute('aria-busy', 'false'); return;
    }
    savedHost.setAttribute('aria-busy', 'true');
    try {
        const response = await fetch('/phong/du-lieu?ids=' + saved.ids.join(','), { cache: 'no-store', signal: request.signal });
        if (!response.ok) throw new Error('Could not load rooms');
        const data = await response.json();
        if (!Array.isArray(data.rooms) || !Array.isArray(data.unavailableIds)) throw new Error('Unexpected room data');
        const content = document.createDocumentFragment();
        const heading = node('p', 'room-saved-summary', data.rooms.length + ' phòng đã lưu đang công khai'); content.append(heading);
        if (data.unavailableIds.length) {
            const notice = node('div', 'guest-preview-notice');
            notice.append(node('p', '', data.unavailableIds.length + ' phòng đã lưu không còn công khai hoặc không còn trống.'));
            const remove = node('button', 'room-text-button', 'Bỏ các phòng không còn công khai'); remove.type = 'button'; remove.dataset.removeUnavailable = data.unavailableIds.join(','); notice.append(remove); content.append(notice);
        }
        const grid = node('div', 'guest-room-grid');
        data.rooms.filter(room => saved.ids.includes(Number(room.id))).forEach(room => grid.append(roomCard(room)));
        content.append(grid);
        savedHost.replaceChildren(content);
        syncControls();
    } catch (error) {
        if (error.name === 'AbortError') return;
        const notice = node('div', 'guest-preview-notice'); notice.setAttribute('role', 'alert');
        notice.append(node('p', '', 'Chưa tải được thông tin phòng. Danh sách đã lưu của bạn vẫn được giữ.'));
        const retry = node('button', 'guest-button guest-button-outline', 'Thử lại'); retry.type = 'button'; retry.dataset.retrySaved = ''; notice.append(retry); savedHost.replaceChildren(notice);
    } finally { if (request === savedRequest) savedHost.setAttribute('aria-busy', 'false'); }
}

document.addEventListener('click', async event => {
    const button = event.target.closest('button,a');
    if (!button) return;
    if (button.matches('[data-save-room]')) {
        const result = saved.toggle(button.dataset.saveRoom);
        announce(!result.ok ? 'Bạn có thể lưu tối đa 50 phòng. Hãy bỏ một phòng trước khi thêm.' : result.selected ? 'Đã lưu phòng vào danh sách yêu thích.' : 'Đã bỏ phòng khỏi danh sách đã lưu.');
        syncControls(); persistenceNotice(); await loadSavedRooms();
    } else if (button.matches('[data-clear-saved]')) {
        saved.set([]); syncControls(); await loadSavedRooms(); announce('Đã xóa danh sách phòng lưu trên trình duyệt này.');
    } else if (button.matches('[data-clear-compare]')) {
        comparison.set([]); syncControls(); announce('Đã bỏ chọn các phòng so sánh.');
    } else if (button.matches('[data-compare-link]') && comparison.ids.length < 2) {
        event.preventDefault(); announce('Chọn ít nhất 2 phòng để so sánh.');
    } else if (button.matches('[data-retry-saved]')) {
        await loadSavedRooms();
    } else if (button.matches('[data-remove-unavailable]')) {
        const unavailable = normalizeIds(button.dataset.removeUnavailable.split(','), 50);
        saved.set(saved.ids.filter(id => !unavailable.includes(id))); syncControls(); await loadSavedRooms();
    } else if (button.matches('[data-remove-comparison]')) {
        const ids = normalizeIds((new URLSearchParams(location.search).get('ids') || '').split(','), 3).filter(id => id !== Number(button.dataset.removeComparison));
        comparison.set(ids); location.assign('/phong/so-sanh?ids=' + ids.join(','));
    } else if (button.matches('[data-copy-page]')) {
        try { await navigator.clipboard.writeText(location.href); announce('Đã sao chép liên kết.'); }
        catch {
            const input = node('input', 'room-copy-fallback'); input.value = location.href; input.readOnly = true; input.setAttribute('aria-label', 'Liên kết để sao chép'); button.after(input); input.focus(); input.select(); announce('Hãy sao chép liên kết trong ô vừa mở.');
        }
    }
});
document.addEventListener('change', event => {
    const input = event.target;
    if (input.matches('[data-compare-room]')) {
        const result = comparison.toggle(input.dataset.compareRoom);
        syncControls();
        if (!result.ok) announce('Chỉ so sánh tối đa 3 phòng. Bỏ chọn một phòng để thêm phòng khác.');
        persistenceNotice();
    }
    if (input.matches('[data-room-sort]')) input.form?.requestSubmit();
});
window.addEventListener('storage', event => {
    if (event.key === saved.key || event.key === comparison.key || event.key === null) {
        saved.reload(); comparison.reload(); syncControls(); loadSavedRooms();
    }
});
if (location.pathname === '/phong/so-sanh') {
    const visibleIds = [...document.querySelectorAll('[data-comparison-room]')].map(header => header.dataset.comparisonRoom);
    comparison.set(visibleIds);
}
if (savedHost) restorePendingSelection(saved, new URLSearchParams(location.search).get('ids'));
syncControls(); persistenceNotice(); loadSavedRooms();
