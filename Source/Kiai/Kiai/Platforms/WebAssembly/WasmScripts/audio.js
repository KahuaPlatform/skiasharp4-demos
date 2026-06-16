// Kia'i WASM audio via Web Audio API.
// Mirrors the desktop NAudio voices procedurally — no sample files are bundled.
// AudioContext is created lazily and resumed on the first call so the browser's
// autoplay policy doesn't block the first sound (the user's first key-press
// or click satisfies the gesture requirement).
//
// One-shot voices: playShoot, playExplosion, playHyperspace, playSmartBomb,
//   playHumanoidRescued (rising chime), playHumanoidLost (falling chime),
//   playMutate.
// Looping voice: startThrust / stopThrust — allocates persistent nodes on Start
//   and tears them down with a short fade on Stop.

(function () {
    const NS = (globalThis.kiaiAudio = globalThis.kiaiAudio || {});
    NS.ctx = null;
    NS.thrust = null;
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

    // --- One-shot voices -------------------------------------------------------

    NS.playShoot = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = "square";
        osc.frequency.setValueAtTime(1040, t);
        osc.frequency.exponentialRampToValueAtTime(280, t + 0.08);
        gain.gain.setValueAtTime(0.16, t);
        gain.gain.linearRampToValueAtTime(0, t + 0.08);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + 0.09);
    };

    NS.playExplosion = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 0.4;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        for (let i = 0; i < sampleCount; i++) {
            const noise = Math.random() * 2 - 1;
            filter = filter * 0.65 + noise * 0.35;
            const env = Math.exp(-3.0 * i / sampleCount);
            data[i] = filter * env * 0.6;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };

    NS.playHyperspace = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 0.32;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let phase = 0;
        for (let i = 0; i < sampleCount; i++) {
            const tt = i / sampleCount;
            const freq = 80 + (1500 - 80) * Math.pow(1 - tt, 2.4);
            phase = (phase + freq / ctx.sampleRate) % 1;
            const saw = phase * 2 - 1;
            const noise = (Math.random() * 2 - 1) * 0.2;
            const env = Math.min(1, tt * 10) * Math.exp(-2.2 * tt);
            data[i] = (saw + noise) * env * 0.3;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };

    // Long noise wash + falling tone — the screen-clearing smart bomb.
    NS.playSmartBomb = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 0.7;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0, phase = 0;
        for (let i = 0; i < sampleCount; i++) {
            const tt = i / sampleCount;
            const noise = Math.random() * 2 - 1;
            filter = filter * 0.5 + noise * 0.5;
            const freq = 520 * (1 - tt) + 60;
            phase = (phase + freq / ctx.sampleRate) % 1;
            const tone = phase < 0.5 ? 1 : -1;
            const env = Math.exp(-2.0 * tt);
            data[i] = (filter * 0.5 + tone * 0.3) * env * 0.5;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };

    // Three-note arpeggio: rising == rescue, falling == loss.
    const playChime = function (notes) {
        const ctx = NS.ensure(); if (!ctx) return;
        const t0 = ctx.currentTime;
        const per = 0.12;
        for (let n = 0; n < notes.length; n++) {
            const t = t0 + n * per;
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = "sine";
            osc.frequency.setValueAtTime(notes[n], t);
            gain.gain.setValueAtTime(0, t);
            gain.gain.linearRampToValueAtTime(0.3, t + per * 0.4);
            gain.gain.linearRampToValueAtTime(0, t + per);
            osc.connect(gain).connect(ctx.destination);
            osc.start(t);
            osc.stop(t + per + 0.02);
        }
    };

    NS.playHumanoidRescued = function () { playChime([523.25, 659.25, 783.99]); };
    NS.playHumanoidLost    = function () { playChime([659.25, 523.25, 392.00]); };

    // Warbling rising saw — Lander mutating into a Mutant.
    NS.playMutate = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 0.45;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let phase = 0;
        for (let i = 0; i < sampleCount; i++) {
            const tt = i / sampleCount;
            const freq = 220 + 600 * tt;
            const warble = 1 + 0.25 * Math.sin(2 * Math.PI * 18 * tt);
            phase = (phase + freq * warble / ctx.sampleRate) % 1;
            const saw = phase * 2 - 1;
            const env = Math.sin(Math.PI * tt) * 0.4;
            data[i] = saw * env;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };

    // --- Looping voice: thrust -------------------------------------------------

    NS.startThrust = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        if (NS.thrust) return; // already running
        const t = ctx.currentTime;

        // Pre-render 1 second of bandpass-flavoured noise and loop it.
        const sampleCount = ctx.sampleRate;
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        for (let i = 0; i < sampleCount; i++) {
            const noise = Math.random() * 2 - 1;
            filter = filter * 0.82 + noise * 0.18;
            data[i] = (noise - filter) * 0.30;
        }

        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.loop = true;
        const gain = ctx.createGain();
        gain.gain.setValueAtTime(0, t);
        gain.gain.linearRampToValueAtTime(1.0, t + 0.1);
        src.connect(gain).connect(ctx.destination);
        src.start(t);
        NS.thrust = { src, gain };
    };

    NS.stopThrust = function () {
        if (!NS.thrust || !NS.ctx) return;
        const t = NS.ctx.currentTime;
        const { src, gain } = NS.thrust;
        gain.gain.cancelScheduledValues(t);
        gain.gain.setValueAtTime(gain.gain.value, t);
        gain.gain.linearRampToValueAtTime(0, t + 0.1);
        src.stop(t + 0.12);
        NS.thrust = null;
    };
})();
