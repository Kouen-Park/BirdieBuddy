// User/round-scoped durable outbox. DOM code never owns the authoritative queue.
class LiveDraftStore {
  constructor(storage, userId, roundId) {
    this.storage = storage;
    this.userId = Number(userId);
    this.roundId = Number(roundId);
    this.key = `birdiebuddy.live.v2.${this.userId}.${this.roundId}`;
    this.inFlight = null;
  }

  read() {
    const raw = this.storage.getItem(this.key);
    if (!raw) return null;
    const state = JSON.parse(raw);
    if (state.version !== 2 || state.userId !== this.userId || state.round.id !== this.roundId || !state.edits)
      throw new Error('The local draft could not be read. Do not clear browser data; keep it for recovery.');
    return state;
  }

  write(state) {
    try { this.storage.setItem(this.key, JSON.stringify(state)); }
    catch { throw new Error('Device storage is unavailable or full. Keep this page open until your changes can be saved.'); }
  }

  seed(round, course) {
    const previous = this.read();
    this.write({ version: 2, userId: this.userId, round, course,
      currentHole: previous?.currentHole ?? round.currentHole,
      edits: previous?.edits || {} });
  }

  queue(holeNumber, payload) {
    const state = this.read();
    if (!state) throw new Error('Open the round online before editing it.');
    const previous = state.edits[holeNumber];
    state.edits[holeNumber] = {
      revision: globalThis.crypto.randomUUID(),
      payload: { ...payload },
      expected: previous ? previous.expected : state.round.holes.find(h => h.holeNumber === holeNumber) || null
    };
    state.currentHole = holeNumber;
    this.write(state); // If persistence fails, never announce that the input is safe.
  }

  navigate(holeNumber) {
    const state = this.read();
    state.currentHole = holeNumber;
    this.write(state);
  }

  pending() { return Object.keys(this.read()?.edits || {}).length; }

  async resolve(number, revision, serverRound, choice) {
    if (!['server', 'local'].includes(choice)) throw new Error('Choose a conflict resolution.');
    await this.inFlight?.catch(() => {});
    const apply = async () => {
      const state = this.read();
      if (serverRound.id !== this.roundId || serverRound.status !== 'Draft')
        throw new Error('This round is no longer editable. Your device copy is preserved.');
      if (state?.edits[number]?.revision !== revision)
        throw new Error('Your local input changed. Close this comparison and review it again.');
      const serverHole = serverRound.holes.find(h => h.holeNumber === number) || null;
      state.round = serverRound;
      if (choice === 'server') delete state.edits[number];
      else {
        state.edits[number].expected = serverHole;
        state.edits[number].revision = globalThis.crypto.randomUUID();
      }
      this.write(state);
    };
    return globalThis.navigator?.locks ? globalThis.navigator.locks.request(this.key, apply) : apply();
  }

  view() {
    const state = this.read();
    if (!state) return null;
    const holes = new Map(state.round.holes.map(h => [h.holeNumber, h]));
    for (const [number, edit] of Object.entries(state.edits))
      holes.set(Number(number), { holeNumber: Number(number), ...edit.payload });
    return { ...state, round: { ...state.round, holes: [...holes.values()] } };
  }

  flush(send, verifyUser) {
    if (this.inFlight) return this.inFlight;
    const drain = async () => {
      for (;;) {
        const state = this.read();
        const entry = Object.entries(state?.edits || {})[0];
        if (!entry) return;
        if (Number(await verifyUser()) !== this.userId)
          throw new Error('Your signed-in account changed. Sign back in to the original account to sync this draft.');
        const [number, sent] = entry;
        let saved;
        try { saved = await send(Number(number), { ...sent.payload, checkExpected: true, expectedHole: sent.expected }); }
        catch (error) { if (error.status === 409) error.holeNumber = Number(number); throw error; }
        const latest = this.read();
        if (!latest) return;
        latest.round.holes = latest.round.holes.filter(h => h.holeNumber !== Number(number)).concat(saved);
        const pending = latest.edits[number];
        if (pending?.revision === sent.revision) delete latest.edits[number];
        else if (pending) pending.expected = saved; // Preserve newer input typed while the request was running.
        this.write(latest);
      }
    };
    const run = () => globalThis.navigator?.locks
      ? globalThis.navigator.locks.request(this.key, drain)
      : drain();
    this.inFlight = Promise.resolve().then(run).finally(() => { this.inFlight = null; });
    return this.inFlight;
  }
}

if (typeof module !== 'undefined') module.exports = { LiveDraftStore };
