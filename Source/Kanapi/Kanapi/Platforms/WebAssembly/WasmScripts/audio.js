// Kanapi WASM audio via Web Audio API.
// Mirrors the desktop NAudio voices procedurally — no sample files bundled.
// AudioContext is gated on a user gesture (autoplay policy); until then, all
// audio calls no-op silently.

(function () {
    const NS = (globalThis.kanapiAudio = globalThis.kanapiAudio || {});
    NS.ctx = null;
    NS.gestureReceived = false;

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

    NS.playShoot = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = "square";
        osc.frequency.setValueAtTime(1300, t);
        osc.frequency.exponentialRampToValueAtTime(500, t + 0.06);
        gain.gain.setValueAtTime(0.14, t);
        gain.gain.linearRampToValueAtTime(0, t + 0.06);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + 0.07);
    };

    NS.playMushroomHit = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = "sine";
        osc.frequency.setValueAtTime(220, t);
        gain.gain.setValueAtTime(0.20, t);
        gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.06);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + 0.07);
    };

    NS.playSegmentKill = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 0.16;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        for (let i = 0; i < sampleCount; i++) {
            const tt = i / sampleCount;
            const noise = Math.random() * 2 - 1;
            filter = filter * 0.7 + noise * 0.3;
            const ang = 2 * Math.PI * 180 * i / ctx.sampleRate;
            const env = Math.exp(-7 * tt);
            data[i] = (filter * 0.5 + Math.sin(ang) * 0.4) * env;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };

    NS.playSpiderKill = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = "sawtooth";
        osc.frequency.setValueAtTime(880, t);
        osc.frequency.exponentialRampToValueAtTime(240, t + 0.22);
        gain.gain.setValueAtTime(0.30, t);
        gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.22);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + 0.23);
    };

    NS.playPlayerDeath = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 0.6;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        let phase = 0;
        for (let i = 0; i < sampleCount; i++) {
            const tt = i / sampleCount;
            const noise = Math.random() * 2 - 1;
            filter = filter * 0.8 + noise * 0.2;
            const freq = 220 - tt * 160;
            phase = (phase + freq / ctx.sampleRate) % 1;
            const saw = phase * 2 - 1;
            const env = Math.exp(-2.5 * tt);
            data[i] = (filter * 0.5 + saw * 0.3) * env;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };
})();
