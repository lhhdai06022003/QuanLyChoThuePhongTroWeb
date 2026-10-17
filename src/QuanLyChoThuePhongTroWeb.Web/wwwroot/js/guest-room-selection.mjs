export function normalizeIds(value, limit) {
    if (!Array.isArray(value)) return [];
    const decimalIds = value.filter(id => typeof id === 'number' ||
        typeof id === 'string' && /^[1-9]\d*$/.test(id.trim()));
    return [...new Set(decimalIds.map(Number).filter(id => Number.isSafeInteger(id) && id > 0))].slice(0, limit);
}

export function restorePendingSelection(store, value) {
    if (typeof value === 'string') store.set(value.split(','));
}

export class SelectionStore {
    constructor(storage, key, limit) {
        this.storage = storage;
        this.key = key;
        this.limit = limit;
        this.ids = [];
        this.persistent = true;
        this.reload();
    }

    reload() {
        try {
            const raw = this.storage.getItem(this.key);
            try { this.ids = normalizeIds(raw ? JSON.parse(raw) : [], this.limit); }
            catch { this.ids = []; }
        } catch { this.persistent = false; }
    }

    set(ids) {
        this.ids = normalizeIds(ids, this.limit);
        try { this.storage.setItem(this.key, JSON.stringify(this.ids)); }
        catch { this.persistent = false; }
    }

    toggle(value) {
        const id = Number(value);
        if (!Number.isSafeInteger(id) || id <= 0) return { ok: false, reason: 'invalid' };
        const exists = this.ids.includes(id);
        if (!exists && this.ids.length >= this.limit) return { ok: false, reason: 'limit' };
        this.set(exists ? this.ids.filter(item => item !== id) : [...this.ids, id]);
        return { ok: true, selected: !exists };
    }
}
