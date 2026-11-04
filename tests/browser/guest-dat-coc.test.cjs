const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname,
    '../../src/QuanLyChoThuePhongTroWeb.Web/wwwroot/js/guest-dat-coc.js'), 'utf8');

function page(fetch) {
    const listeners = new Map();
    const button = {
        disabled: false, innerHTML: '<span>Thanh toán VNPay</span>',
        addEventListener(name, handler) { listeners.set(name, handler); },
        get textContent() { return this.innerHTML.replace(/<[^>]+>/g, ''); },
        set textContent(value) { this.innerHTML = value; },
        appendChild(node) { this.innerHTML += node.html; node.parent.firstChild = null; }
    };
    const alerts = [];
    const redirects = [];
    const document = {
        querySelectorAll: () => [],
        querySelector(selector) {
            if (selector === '#btnGuestVnPay') return button;
            if (selector === 'input[name="__RequestVerificationToken"]') return { value: 'csrf-token' };
            return null;
        },
        getElementById(id) { return id === 'btnGuestVnPay' ? button : null; },
        createElement() {
            return { firstChild: null, insertAdjacentHTML(position, html) {
                this.firstChild = { html, parent: this };
            } };
        },
        addEventListener() {}
    };
    const window = { location: { assign: url => redirects.push(url), reload() {} } };
    vm.runInNewContext(source, { window, document, navigator: {}, fetch,
        alert: message => alerts.push(message), setTimeout, console });
    window.GuestDatCoc.init(42);
    return { button, alerts, redirects, click: () => listeners.get('click').call(button) };
}

const flush = () => new Promise(resolve => setImmediate(resolve));

test('checkout initializes without jQuery and posts the authenticated request once', async () => {
    const calls = [];
    let resolveResponse;
    const app = page((url, options) => {
        calls.push({ url, options });
        return new Promise(resolve => { resolveResponse = resolve; });
    });
    app.click();
    app.click();
    assert.equal(app.button.disabled, true);
    assert.equal(calls.length, 1);
    assert.equal(calls[0].url, '/tai-khoan/yeu-cau-phong/giu-cho/42/dat-coc/vnpay');
    assert.equal(calls[0].options.method, 'POST');
    assert.equal(calls[0].options.headers.RequestVerificationToken, 'csrf-token');
    resolveResponse({ ok: true, json: async () => ({ success: true, url: 'https://sandbox.vnpayment.vn/pay' }) });
    await flush();
    assert.deepEqual(app.redirects, ['https://sandbox.vnpayment.vn/pay']);
    assert.deepEqual(app.alerts, []);
});

test('a rejected checkout response restores the button without an unhandled rejection', async () => {
    const app = page(async () => ({ ok: false, status: 409,
        json: async () => ({ success: false, message: 'Lượt thanh toán đã hết hạn.' }) }));
    app.click();
    await flush();
    assert.equal(app.button.disabled, false);
    assert.equal(app.button.innerHTML, '<span>Thanh toán VNPay</span>');
    assert.deepEqual(app.alerts, ['Lượt thanh toán đã hết hạn.']);
    assert.deepEqual(app.redirects, []);
});

test('network failures and unsafe redirect URLs leave checkout retryable', async () => {
    for (const fetch of [async () => { throw new Error('offline'); },
        async () => ({ ok: true, json: async () => ({ success: true, url: 'javascript:alert(1)' }) })]) {
        const app = page(fetch);
        app.click();
        await flush();
        assert.equal(app.button.disabled, false);
        assert.equal(app.alerts.length, 1);
        assert.deepEqual(app.redirects, []);
    }
});
