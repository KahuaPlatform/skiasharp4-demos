// Mahina WASM audio via Web Audio API.
// Mirrors the desktop NAudio voices procedurally — no sample files are bundled.
// AudioContext is gated on a user gesture (autoplay policy); until then, all
// audio calls no-op silently.

(function () {
    const NS = (globalThis.mahinaAudio = globalThis.mahinaAudio || {});
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

    NS.playExplosion = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 0.45;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        for (let i = 0; i < sampleCount; i++) {
            const noise = Math.random() * 2 - 1;
            filter = filter * 0.72 + noise * 0.28;
            const env = Math.exp(-2.8 * i / sampleCount);
            data[i] = filter * env * 0.6;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };

    // C5 -> E5 -> G5 -> C6 triangle-wave arpeggio over ~0.6s, gentle decay per peg.
    NS.playLandingChime = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const freqs = [523.25, 659.25, 783.99, 1046.50];
        const noteDur = 0.15;
        for (let n = 0; n < freqs.length; n++) {
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = "triangle";
            osc.frequency.setValueAtTime(freqs[n], t + n * noteDur);
            gain.gain.setValueAtTime(0.22, t + n * noteDur);
            gain.gain.exponentialRampToValueAtTime(0.0001, t + (n + 1) * noteDur);
            osc.connect(gain).connect(ctx.destination);
            osc.start(t + n * noteDur);
            osc.stop(t + (n + 1) * noteDur + 0.02);
        }
    };

    // Looping rocket thrust. Pre-render 1s of bandpass-flavoured noise, loop it,
    // gain-modulated by an LFO pulse with short fade-in/out.
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
            data[i] = ((noise - filter) * 0.5 + filter * 0.5) * 0.35;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.loop = true;

        const gain = ctx.createGain();
        gain.gain.setValueAtTime(0, t);
        gain.gain.linearRampToValueAtTime(1.0, t + 0.05);

        const lfo = ctx.createOscillator();
        const lfoDepth = ctx.createGain();
        lfo.frequency.setValueAtTime(4, t);
        lfoDepth.gain.setValueAtTime(0.15, t);
        lfo.connect(lfoDepth).connect(gain.gain);
        lfo.start(t);

        src.connect(gain).connect(ctx.destination);
        src.start(t);
        NS.thrust = { src, gain, lfo };
    };

    NS.stopThrust = function () {
        if (!NS.thrust || !NS.ctx) return;
        const t = NS.ctx.currentTime;
        const { src, gain, lfo } = NS.thrust;
        gain.gain.cancelScheduledValues(t);
        gain.gain.setValueAtTime(gain.gain.value, t);
        gain.gain.linearRampToValueAtTime(0, t + 0.1);
        src.stop(t + 0.12);
        lfo.stop(t + 0.12);
        NS.thrust = null;
    };
})();
