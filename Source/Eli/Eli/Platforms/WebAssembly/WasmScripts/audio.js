// Eli WASM audio via Web Audio API.
// Mirrors the desktop NAudio voices procedurally, voice for voice — no sample
// files are bundled. AudioContext is created lazily and resumed on the first call
// so the browser's autoplay policy doesn't block the first sound (the user's first
// key-press or click satisfies the gesture requirement).
//
// One-shot voices: playDig, playHarpoonFire, playHarpoonStick, playPump,
// playBurst, playPhase, playRockWobble, playRockFall, playRockShatter, playDeath,
// playLevelClear.

(function () {
    const NS = (globalThis.eliAudio = globalThis.eliAudio || {});
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

    // A short filtered-noise burst. `lp` is the one-pole lowpass coefficient,
    // mirroring the desktop NoiseBurst: high = muffled earth, low = sharp crack.
    function noise(dur, amp, lp) {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const n = Math.floor(ctx.sampleRate * dur);
        const buffer = ctx.createBuffer(1, n, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        for (let i = 0; i < n; i++) {
            const w = Math.random() * 2 - 1;
            filter = filter * lp + w * (1 - lp);
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

    NS.playDig          = function ()  { noise(0.07, 0.13, 0.82); };
    NS.playHarpoonFire  = function ()  { blip("square",   300, 900, 0.09, 0.17); };
    NS.playHarpoonStick = function ()  { blip("square",   900, 420, 0.05, 0.15); };
    // Pitch is passed in from the sim and rises with inflation, so the ear tracks
    // how close the monster is to popping.
    NS.playPump         = function (f) { blip("square",   f || 340, (f || 340) * 1.45, 0.10, 0.16); };
    NS.playBurst        = function ()  { noise(0.28, 0.45, 0.5); };
    NS.playPhase        = function ()  { blip("sine",     620, 210, 0.30, 0.10); };
    NS.playRockWobble   = function ()  { arp([150, 130, 150, 130], 0.55); };
    NS.playRockFall     = function ()  { noise(0.45, 0.40, 0.88); };
    NS.playRockShatter  = function ()  { noise(0.22, 0.50, 0.3); };
    NS.playLevelClear   = function ()  { arp([392, 493.88, 587.33, 783.99, 1046.5], 0.7); };

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
