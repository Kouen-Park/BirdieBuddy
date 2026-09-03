const { test } = require('node:test');
const assert = require('node:assert/strict');
const { LiveDraftStore } = require('../../wwwroot/js/live-store.js');

class MemoryStorage {
  data = new Map();
  getItem(key) { return this.data.get(key) ?? null; }
  setItem(key, value) { this.data.set(key, value); }
}
const score = value => ({ par: 4, score: value, putts: 2, gir: false, fairwayHit: null, penalty: 0 });
function setup(storage = new MemoryStorage(), userId = 7, roundId = 10) {
  const store = new LiveDraftStore(storage, userId, roundId);
  store.seed({ id: roundId, currentHole: 1, holes: [], status: 'Draft' }, { id: 1 });
  return store;
}
const owner = async () => 7;

test('choosing server drops only the reviewed revision and preserves other pending holes', async () => {
  const store = setup(); store.queue(1, score(6)); store.queue(2, score(5));
  const remote = { ...store.read().round, holes: [{ holeNumber: 1, ...score(4) }] };
  await store.resolve(1, store.read().edits[1].revision, remote, 'server');
  assert.equal(store.pending(), 1);
  assert.equal(store.view().round.holes.find(h => h.holeNumber === 1).score, 4);
  assert.equal(store.view().round.holes.find(h => h.holeNumber === 2).score, 5);
});

test('keeping local input rebases guarded retry onto reviewed server snapshot', async () => {
  const store = setup(); store.queue(1, score(6));
  const remote = { ...store.read().round, holes: [{ holeNumber: 1, ...score(4) }] };
  await store.resolve(1, store.read().edits[1].revision, remote, 'local');
  await store.flush(async (number, data) => {
    assert.equal(data.score, 6); assert.equal(data.expectedHole.score, 4); assert.equal(data.checkExpected, true);
    return savedHole(number, data);
  }, owner);
  assert.equal(store.pending(), 0);
});

test('resolution refuses newer local input, wrong round and terminal state', async () => {
  const store = setup(); store.queue(1, score(6));
  const revision = store.read().edits[1].revision;
  store.queue(1, score(7));
  await assert.rejects(store.resolve(1, revision, store.read().round, 'server'), /local input changed/);
  await assert.rejects(store.resolve(1, store.read().edits[1].revision, { ...store.read().round, status: 'Completed' }, 'server'), /no longer editable/);
  await assert.rejects(store.resolve(1, store.read().edits[1].revision, { ...store.read().round, id: 999 }, 'server'));
  assert.equal(store.view().round.holes[0].score, 7);
});

test('conflict identifies the affected hole and failed resolution storage keeps local input', async () => {
  const store = setup(); store.queue(2, score(6));
  const failure = Object.assign(new Error('Conflict'), { status: 409 });
  await assert.rejects(store.flush(async () => { throw failure; }, owner), error => error.holeNumber === 2);
  const state = store.read();
  store.storage.setItem = () => { throw new Error('Full'); };
  await assert.rejects(store.resolve(2, state.edits[2].revision, state.round, 'server'), /storage is unavailable/);
  assert.equal(store.pending(), 1);
});
const savedHole = (number, data) => ({ id: number, holeNumber: number, par: data.par, score: data.score,
  putts: data.putts, gir: data.gir, fairwayHit: data.fairwayHit, penalty: data.penalty });

test('input is durable before navigation and survives reopening', () => {
  const store = setup();
  store.queue(1, score(5));
  store.navigate(2);
  const restored = new LiveDraftStore(store.storage, 7, 10);
  assert.equal(restored.view().round.holes[0].score, 5);
  assert.equal(restored.view().currentHole, 2);
  assert.equal(restored.pending(), 1);
});

test('user and round queues cannot read one another', () => {
  const store = setup();
  store.queue(1, score(5));
  assert.equal(new LiveDraftStore(store.storage, 8, 10).read(), null);
  assert.equal(new LiveDraftStore(store.storage, 7, 11).read(), null);
});

test('editing while save is in flight preserves and sends the latest revision', async () => {
  const store = setup();
  store.queue(1, score(5));
  let release;
  let started;
  const firstStarted = new Promise(resolve => { started = resolve; });
  const gate = new Promise(resolve => { release = resolve; });
  const requests = [];
  const send = async (number, data) => {
    requests.push(data);
    if (requests.length === 1) { started(); await gate; }
    return savedHole(number, data);
  };
  const sync = store.flush(send, owner);
  await firstStarted;
  store.queue(1, score(6));
  release();
  await sync;
  assert.deepEqual(requests.map(x => x.score), [5, 6]);
  assert.equal(requests[1].expectedHole.score, 5);
  assert.equal(store.pending(), 0);
  assert.equal(store.view().round.holes[0].score, 6);
});

test('duplicate flush calls share a single in-flight operation', async () => {
  const store = setup();
  store.queue(1, score(5));
  let requests = 0;
  const send = async (number, data) => { requests++; return savedHole(number, data); };
  const first = store.flush(send, owner);
  assert.equal(store.flush(send, owner), first);
  await first;
  assert.equal(requests, 1);
});

test('network failure preserves the outbox for retry', async () => {
  const store = setup();
  store.queue(1, score(5));
  await assert.rejects(store.flush(async () => { throw new Error('offline'); }, owner));
  assert.equal(store.pending(), 1);
  await store.flush(async (number, data) => savedHole(number, data), owner);
  assert.equal(store.pending(), 0);
});

test('account switch stops sync without sending or dropping data', async () => {
  const store = setup();
  store.queue(1, score(5));
  let sent = false;
  await assert.rejects(store.flush(async () => { sent = true; }, async () => 8), /account changed/);
  assert.equal(sent, false);
  assert.equal(store.pending(), 1);
});

test('server conflict keeps the original baseline and local input', async () => {
  const store = setup();
  store.queue(1, score(5));
  await assert.rejects(store.flush(async () => { throw new Error('Conflict'); }, owner));
  const edit = store.read().edits[1];
  assert.equal(edit.expected, null);
  assert.equal(edit.payload.score, 5);
});

test('storage failure is explicit, never a false saved acknowledgement', () => {
  const store = setup();
  store.storage.setItem = () => { throw new Error('quota'); };
  assert.throws(() => store.queue(1, score(5)), /storage is unavailable or full/);
  assert.equal(store.pending(), 0);
});

test('corrupt data is preserved instead of silently reset', () => {
  const store = setup();
  store.storage.setItem(store.key, '{broken');
  assert.throws(() => store.seed({ id: 10, holes: [] }, {}));
  assert.equal(store.storage.getItem(store.key), '{broken');
});
