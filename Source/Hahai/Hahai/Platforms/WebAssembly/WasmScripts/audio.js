// Hahai WASM audio via Web Audio API.
// Mirrors the desktop NAudio voices procedurally — no sample files bundled.
// AudioContext is gated on a user gesture (autoplay policy); until then, all
// audio calls no-op silently.

(function () {
    const NS = (globalThis.hahaiAudio = globalThis.hahaiAudio || {});
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

    NS.playChomp = function (freq) {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = "triangle";
        osc.frequency.setValueAtTime(freq || 720, t);
        gain.gain.setValueAtTime(0.18, t);
        gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.08);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + 0.10);
    };

    function playArpeggio(freqs, totalDuration) {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const noteDur = totalDuration / freqs.length;
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
    }

    NS.playPower      = () => playArpeggio([261.63, 329.63, 392.00, 523.25], 0.55);
    NS.playEatGhost   = () => playArpeggio([523.25, 659.25, 783.99, 1046.50], 0.35);
    NS.playLevelClear = () => playArpeggio([392.00, 493.88, 587.33, 783.99, 1046.50], 0.7);

    NS.playDeath = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 1.2;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let phase = 0;
        for (let i = 0; i < sampleCount; i++) {
            const tt = i / sampleCount;
            const freq = 900 - tt * 700;
            const wobble = 1 + 0.18 * Math.sin(Math.PI * 2 * tt * 14);
            phase = (phase + (freq * wobble) / ctx.sampleRate) % 1;
            const saw = phase * 2 - 1;
            const env = Math.exp(-1.5 * tt);
            data[i] = saw * env * 0.22;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };
})();
