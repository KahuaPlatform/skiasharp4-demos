// Alaloa WASM audio via Web Audio API.
// Mirrors the desktop NAudio voices procedurally — no sample files bundled.
// AudioContext is gated on a user gesture (autoplay policy); until then, all
// audio calls no-op silently.

(function () {
    const NS = (globalThis.alaloaAudio = globalThis.alaloaAudio || {});
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

    NS.playTurn = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = "sine";
        osc.frequency.setValueAtTime(1400, t);
        gain.gain.setValueAtTime(0.16, t);
        gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.03);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + 0.04);
    };

    NS.playCrash = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 0.5;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        let phase = 0;
        for (let i = 0; i < sampleCount; i++) {
            const tt = i / sampleCount;
            const noise = Math.random() * 2 - 1;
            filter = filter * 0.72 + noise * 0.28;
            const freq = 320 - tt * 240;
            phase = (phase + freq / ctx.sampleRate) % 1;
            const saw = phase * 2 - 1;
            const env = Math.exp(-3 * tt);
            data[i] = (filter * 0.5 + saw * 0.4) * env;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };

    function playArpeggio(ctx, t, freqs, noteDur) {
        for (let n = 0; n < freqs.length; n++) {
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = "triangle";
            osc.frequency.setValueAtTime(freqs[n], t + n * noteDur);
            gain.gain.setValueAtTime(0.20, t + n * noteDur);
            gain.gain.exponentialRampToValueAtTime(0.0001, t + (n + 1) * noteDur);
            osc.connect(gain).connect(ctx.destination);
            osc.start(t + n * noteDur);
            osc.stop(t + (n + 1) * noteDur + 0.02);
        }
    }

    NS.playRoundWin = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        playArpeggio(ctx, ctx.currentTime, [523.25, 659.25, 783.99, 1046.50], 0.14);
    };

    NS.playRoundLose = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        playArpeggio(ctx, ctx.currentTime, [783.99, 659.25, 523.25, 392.00], 0.14);
    };
})();
