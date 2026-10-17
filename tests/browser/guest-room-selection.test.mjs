import test from 'node:test';
import assert from 'node:assert/strict';
import { SelectionStore, normalizeIds, restorePendingSelection } from '../../src/QuanLyChoThuePhongTroWeb.Web/wwwroot/js/guest-room-selection.mjs';

const memoryStorage = (initial = null) => ({
    value: initial,
    getItem() { return this.value; },
    setItem(key, value) { this.value = value; }
});

test('normalizes corrupted IDs, duplicates and limits', () => {
    assert.deepEqual(normalizeIds([3, -1, 3, null, 2.5, '4', 5, 6], 3), [3, 4, 5]);
    assert.deepEqual(normalizeIds({ id: 1 }, 3), []);
});
test('does not turn booleans, objects or non-decimal strings into room IDs', () => {
    assert.deepEqual(normalizeIds([true, {}, [], '1e2', '0x10', 1], 3), [1]);
});
test('comparison limit allows remove then add', () => {
    const store = new SelectionStore(memoryStorage(), 'compare', 3);
    [1, 2, 3].forEach(id => assert.equal(store.toggle(id).ok, true));
    assert.equal(store.toggle(4).ok, false);
    assert.deepEqual(store.ids, [1, 2, 3]);
    store.toggle(2); store.toggle(4);
    assert.deepEqual(store.ids, [1, 3, 4]);
});
test('persists only IDs and reads back without stale metadata', () => {
    const storage = memoryStorage();
    new SelectionStore(storage, 'saved', 50).toggle(32);
    assert.equal(storage.value, '[32]');
    assert.deepEqual(new SelectionStore(storage, 'saved', 50).ids, [32]);
});
test('invalid JSON recovers to empty list', () => {
    const store = new SelectionStore(memoryStorage('{broken'), 'saved', 50);
    assert.deepEqual(store.ids, []);
    assert.equal(store.toggle(1).ok, true);
});
test('storage denial keeps page memory and reports unavailable persistence', () => {
    const storage = { getItem() { throw new Error('denied'); }, setItem() { throw new Error('quota'); } };
    const store = new SelectionStore(storage, 'saved', 50);
    store.toggle(10);
    assert.deepEqual(store.ids, [10]);
    assert.equal(store.persistent, false);
});
test('reload observes another tab removal', () => {
    const storage = memoryStorage('[1,2]');
    const store = new SelectionStore(storage, 'saved', 50);
    storage.value = '[2]'; store.reload();
    assert.deepEqual(store.ids, [2]);
});
test('fallback URL restores selection even when storage reads succeed but writes fail', () => {
    const storage = { getItem() { return '[10]'; }, setItem() { throw new Error('Quota exceeded'); } };
    const source = new SelectionStore(storage, 'saved', 50);
    source.toggle(32);
    const destination = new SelectionStore(storage, 'saved', 50);
    assert.equal(destination.persistent, true);
    restorePendingSelection(destination, source.ids.join(','));
    assert.deepEqual(destination.ids, [10, 32]);
    assert.equal(destination.persistent, false);
});
