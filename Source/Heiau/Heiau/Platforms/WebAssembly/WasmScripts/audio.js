// Heiau WASM audio via Web Audio API.
// Mirrors the desktop NAudio voices procedurally — no sample files bundled.
// AudioContext is gated on a user gesture (autoplay policy); until then, all
// audio calls no-op silently.

(function () {
    const NS = (globalThis.heiauAudio = globalThis.heiauAudio || {});
    NS.ctx = null;
    NS.thrust = null;
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
        osc.frequency.setValueAtTime(1000, t);
        osc.frequency.exponentialRampToValueAtTime(300, t + 0.07);
        gain.gain.setValueAtTime(0.18, t);
        gain.gain.linearRampToValueAtTime(0, t + 0.07);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + 0.08);
    };

    NS.playRingHit = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const osc1 = ctx.createOscillator();
        const osc2 = ctx.createOscillator();
        const gain = ctx.createGain();
        osc1.type = "sine"; osc1.frequency.setValueAtTime(1320, t);
        osc2.type = "sine"; osc2.frequency.setValueAtTime(1980, t);
        gain.gain.setValueAtTime(0.18, t);
        gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.18);
        osc1.connect(gain);
        osc2.connect(gain);
        gain.connect(ctx.destination);
        osc1.start(t); osc2.start(t);
        osc1.stop(t + 0.19); osc2.stop(t + 0.19);
    };

    NS.playTurretFire = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = "sawtooth";
        osc.frequency.setValueAtTime(260, t);
        osc.frequency.exponentialRampToValueAtTime(80, t + 0.15);
        gain.gain.setValueAtTime(0.30, t);
        gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.18);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + 0.19);
    };

    NS.playTurretKill = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 1.0;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        let phase = 0;
        for (let i = 0; i < sampleCount; i++) {
            const tt = i / sampleCount;
            const freq = 900 * Math.pow(1 - tt, 1.4) + 80;
            phase = (phase + freq / ctx.sampleRate) % 1;
            const saw = phase * 2 - 1;
            const noise = Math.random() * 2 - 1;
            filter = filter * 0.85 + noise * 0.15;
            const env = Math.min(1, tt * 12) * Math.exp(-2.0 * tt);
            data[i] = (saw * 0.55 + filter * 0.35) * env;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };

    NS.playShipExplosion = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 0.4;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        for (let i = 0; i < sampleCount; i++) {
            const noise = Math.random() * 2 - 1;
            filter = filter * 0.72 + noise * 0.28;
            const env = Math.exp(-3.0 * i / sampleCount);
            data[i] = filter * env * 0.6;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };

    NS.startThrust = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        if (NS.thrust) return;
        const t = ctx.currentTime;
        const sampleCount = ctx.sampleRate;
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        for (let i = 0; i < sampleCount; i++) {
            const noise = Math.random() * 2 - 1;
            filter = filter * 0.85 + noise * 0.15;
            data[i] = ((noise - filter) * 0.5 + filter * 0.5) * 0.28;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.loop = true;

        const gain = ctx.createGain();
        gain.gain.setValueAtTime(0, t);
        gain.gain.linearRampToValueAtTime(1.0, t + 0.05);
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
