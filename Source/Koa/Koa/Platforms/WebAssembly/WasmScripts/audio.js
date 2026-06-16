// Koa WASM audio via Web Audio API.
// Mirrors the desktop NAudio voices procedurally — no sample files are bundled.
// AudioContext is created lazily and resumed on the first call so the browser's
// autoplay policy doesn't block the first sound (the user's first key-press
// or click satisfies the gesture requirement).
//
// One-shot voices: playShoot, playHit, playEnemyDie, playGeneratorDie,
// playPickup, playDoor, playPotion, playHeroHurt, playDeath, playLevelClear,
// playLowHealth ("warrior needs food badly").

(function () {
    const NS = (globalThis.koaAudio = globalThis.koaAudio || {});
    NS.ctx = null;
    NS.gestureReceived = false;

    // Browser autoplay policy bans AudioContext creation/resume before a user
    // gesture. Arm a one-time listener on common gestures; until it fires, all
    // audio calls no-op silently (no console warnings).
    const arm = () => {
        if (NS.gestureReceived) return;
        NS.gestureReceived = true;
        try {
            const AC = window.AudioContext || window.webkitAudioContext;
            NS.ctx = new AC();
            if (NS.ctx.state === "suspended") NS.ctx.resume();
        } catch (e) { /* fail silent */ }
        window.removeEventListener('pointerdown', arm, true);
        window.removeEventListener('keydown',     arm, true);
        window.removeEventListener('touchstart',  arm, true);
    };
    window.addEventListener('pointerdown', arm, true);
    window.addEventListener('keydown',     arm, true);
    window.addEventListener('touchstart',  arm, true);

    NS.ensure = function () {
        if (!NS.gestureReceived || !NS.ctx) return null;
        if (NS.ctx.state === "suspended") NS.ctx.resume();
        return NS.ctx;
    };

    // --- Small helpers ---------------------------------------------------------

    // A pitch-swept oscillator blip with an exponential decay envelope.
    function blip(type, f0, f1, dur, amp) {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = type;
        osc.frequency.setValueAtTime(f0, t);
        osc.frequency.exponentialRampToValueAtTime(Math.max(1, f1), t + dur);
        gain.gain.setValueAtTime(amp, t);
        gain.gain.exponentialRampToValueAtTime(0.0001, t + dur);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + dur + 0.02);
    }

    // A short filtered-noise burst (explosions / destruction).
    function noise(dur, amp) {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const n = Math.floor(ctx.sampleRate * dur);
        const buffer = ctx.createBuffer(1, n, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        for (let i = 0; i < n; i++) {
            const w = Math.random() * 2 - 1;
            filter = filter * 0.6 + w * 0.4;
            const env = Math.exp(-3.5 * i / n);
            data[i] = filter * env * amp;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    }

    // An arpeggio of triangle notes (pickups / fanfares).
    function arp(freqs, total) {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const noteDur = total / freqs.length;
        for (let k = 0; k < freqs.length; k++) {
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            const start = t + k * noteDur;
            osc.type = "triangle";
            osc.frequency.setValueAtTime(freqs[k], start);
            gain.gain.setValueAtTime(0.22, start);
            gain.gain.exponentialRampToValueAtTime(0.0001, start + noteDur);
            osc.connect(gain).connect(ctx.destination);
            osc.start(start);
            osc.stop(start + noteDur + 0.02);
        }
    }

    // --- Voices ----------------------------------------------------------------

    NS.playShoot        = function () { blip("square",   760, 300, 0.07, 0.16); };
    NS.playHit          = function () { blip("square",   380, 200, 0.05, 0.12); };
    NS.playEnemyDie     = function () { noise(0.22, 0.45); };
    NS.playGeneratorDie = function () { noise(0.5, 0.7); };
    NS.playPickup       = function () { arp([523.25, 659.25, 783.99], 0.22); };
    NS.playDoor         = function () { blip("sawtooth", 200, 320, 0.25, 0.2); };
    NS.playPotion       = function () { arp([392, 523.25, 659.25, 880], 0.4); };
    NS.playHeroHurt     = function () { blip("square",   300, 120, 0.12, 0.2); };
    NS.playLevelClear   = function () { arp([392, 493.88, 587.33, 783.99, 1046.5], 0.7); };
    NS.playLowHealth    = function () { arp([330, 247], 0.45); };

    NS.playDeath = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const dur = 1.2;
        const n = Math.floor(ctx.sampleRate * dur);
        const buffer = ctx.createBuffer(1, n, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let phase = 0;
        for (let i = 0; i < n; i++) {
            const tt = i / n;
            const freq = 700 - tt * 560;
            const wobble = 1 + 0.18 * Math.sin(Math.PI * 2 * tt * 12);
            phase = (phase + freq * wobble / ctx.sampleRate) % 1;
            const saw = phase * 2 - 1;
            const env = Math.exp(-1.6 * tt);
            data[i] = saw * env * 0.22;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };
})();
