// UnoGalaga WASM audio via Web Audio API.
// Mirrors the desktop NAudio voices procedurally: square-wave shoot bleep,
// lowpass-noise explosion, sawtooth dive sweep. No sample files needed.
//
// The AudioContext is created lazily and resumed on the first call, because
// browser autoplay policy disallows starting audio without a user gesture —
// which the player's first Space-to-fire conveniently satisfies.

(function () {
    const NS = (globalThis.unoGalagaAudio = globalThis.unoGalagaAudio || {});
    NS.ctx = null;

    NS.ensure = function () {
        if (!NS.ctx) {
            try {
                const AC = window.AudioContext || window.webkitAudioContext;
                NS.ctx = new AC();
            } catch (e) { return null; }
        }
        if (NS.ctx.state === "suspended") {
            // Returns a Promise but we don't need to await; the next call will play normally.
            NS.ctx.resume();
        }
        return NS.ctx;
    };

    NS.playShoot = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = "square";
        osc.frequency.setValueAtTime(880, t);
        osc.frequency.exponentialRampToValueAtTime(220, t + 0.08);
        gain.gain.setValueAtTime(0.18, t);
        gain.gain.linearRampToValueAtTime(0, t + 0.08);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + 0.09);
    };

    NS.playExplosion = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const duration = 0.35;
        const sampleCount = Math.floor(ctx.sampleRate * duration);
        const buffer = ctx.createBuffer(1, sampleCount, ctx.sampleRate);
        const data = buffer.getChannelData(0);
        let filter = 0;
        for (let i = 0; i < sampleCount; i++) {
            const noise = Math.random() * 2 - 1;
            filter = filter * 0.65 + noise * 0.35;
            const env = Math.exp(-3 * i / sampleCount);
            data[i] = filter * env * 0.55;
        }
        const src = ctx.createBufferSource();
        src.buffer = buffer;
        src.connect(ctx.destination);
        src.start(t);
    };

    NS.playDive = function () {
        const ctx = NS.ensure(); if (!ctx) return;
        const t = ctx.currentTime;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = "sawtooth";
        osc.frequency.setValueAtTime(500, t);
        osc.frequency.exponentialRampToValueAtTime(100, t + 0.45);
        gain.gain.setValueAtTime(0.0, t);
        gain.gain.linearRampToValueAtTime(0.22, t + 0.05);
        gain.gain.linearRampToValueAtTime(0.0, t + 0.45);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + 0.46);
    };
})();
