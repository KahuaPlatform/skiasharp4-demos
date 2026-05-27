// Lua WASM audio via Web Audio API.
// Mirrors the desktop NAudio voices procedurally — no sample files are bundled.
// AudioContext is created lazily and resumed on the first call so the browser's
// autoplay policy doesn't block the first sound (the user's first key-press
// or click satisfies the gesture requirement).

(function () {
    const NS = (globalThis.luaAudio = globalThis.luaAudio || {});
    NS.ctx = null;

    NS.ensure = function () {
        if (!NS.ctx) {
            try {
                const AC = window.AudioContext || window.webkitAudioContext;
                NS.ctx = new AC();
            } catch (e) { return null; }
        }
        if (NS.ctx.state === "suspended") NS.ctx.resume();
        return NS.ctx;
    };

    NS.playShoot = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = "square";
        osc.frequency.setValueAtTime(1100, t);
        osc.frequency.exponentialRampToValueAtTime(300, t + 0.07);
        gain.gain.setValueAtTime(0.16, t);
        gain.gain.linearRampToValueAtTime(0, t + 0.07);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + 0.08);
    };

    NS.playExplosion = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 0.30;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        for (let i = 0; i < sampleCount; i++) {
            const noise = Math.random() * 2 - 1;
            filter = filter * 0.7 + noise * 0.3;
            const env = Math.exp(-3.2 * i / sampleCount);
            data[i] = filter * env * 0.5;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };

    NS.playFlip = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = "square";
        osc.frequency.setValueAtTime(320, t);
        osc.frequency.setValueAtTime(560, t + 0.02);
        gain.gain.setValueAtTime(0.12, t);
        gain.gain.linearRampToValueAtTime(0, t + 0.04);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + 0.045);
    };

    NS.playZapper = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 0.75;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        for (let i = 0; i < sampleCount; i++) {
            const tt = i / sampleCount;
            const modPhase = ((i * 28) / ctx.sampleRate) % 1;
            const modulator = modPhase < 0.5 ? 1 : -1;
            const baseFreq = 1400 * (1 - tt * 0.85);
            const freq = baseFreq + 240 * modulator;
            const phase = ((i * freq) / ctx.sampleRate) % 1;
            const saw = phase * 2 - 1;
            const env = Math.min(1, tt * 6) * (1 - tt);
            data[i] = saw * env * 0.30;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };

    NS.playWarp = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 2.0;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        for (let i = 0; i < sampleCount; i++) {
            const tt = i / sampleCount;
            const noise = Math.random() * 2 - 1;
            const a = 0.95 - tt * 0.5;
            filter = filter * a + noise * (1 - a);
            const toneFreq = 120 + tt * 1400;
            const phase = ((i * toneFreq) / ctx.sampleRate) % 1;
            const tone = Math.sin(phase * Math.PI * 2);
            const env = Math.min(1, tt * 4) * Math.min(1, (1 - tt) * 4);
            data[i] = (tone * 0.18 + filter * 0.18) * env;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };
})();
