// Pohaku WASM audio via Web Audio API.
// Mirrors the desktop NAudio voices procedurally — no sample files are bundled.
// AudioContext is created lazily and resumed on the first call so the browser's
// autoplay policy doesn't block the first sound (the user's first key-press
// or click satisfies the gesture requirement).
//
// One-shot voices: playShoot, playExplosion, playHyperspace.
// Looping voices: startThrust/stopThrust, startSaucer(large)/stopSaucer. These
// allocate persistent nodes on Start and tear them down with a short fade on Stop.

(function () {
    const NS = (globalThis.pohakuAudio = globalThis.pohakuAudio || {});
    NS.ctx = null;
    NS.thrust = null;
    NS.saucer = null;

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
        // Exponential descent from 1500Hz to 80Hz with sawtooth + noise overlay.
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

    // --- Looping voices --------------------------------------------------------

    // Thrust: bandpass-flavoured white noise. Implemented as a looping noise
    // BufferSource so we don't have to keep generating samples in JS.
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
            data[i] = (noise - filter) * 0.32;
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

    // Saucer hum: dual sine (fundamental + 2nd-harmonic) modulated by an LFO so
    // it warbles. Large saucer 70Hz, small 140Hz — matches arcade convention.
    NS.startSaucer = function (large) {
        const ctx = NS.ensure(); if (!ctx) return;
        if (NS.saucer) return;
        const t = ctx.currentTime;
        const baseFreq = large ? 70 : 140;
        const overFreq = large ? 140 : 280;

        const baseOsc = ctx.createOscillator();
        const overOsc = ctx.createOscillator();
        const baseGain = ctx.createGain();
        const overGain = ctx.createGain();
        baseOsc.type = "sine"; baseOsc.frequency.setValueAtTime(baseFreq, t);
        overOsc.type = "sine"; overOsc.frequency.setValueAtTime(overFreq, t);
        baseGain.gain.setValueAtTime(0.55, t);
        overGain.gain.setValueAtTime(0.20, t);

        // LFO modulates a master gain for the warble.
        const lfo = ctx.createOscillator();
        const lfoDepth = ctx.createGain();
        const master = ctx.createGain();
        lfo.frequency.setValueAtTime(2.8, t);
        lfoDepth.gain.setValueAtTime(0.45, t);
        master.gain.setValueAtTime(0, t);
        master.gain.linearRampToValueAtTime(0.38, t + 0.1);

        // master.gain = 0.38 base + LFO modulation around it
        lfo.connect(lfoDepth).connect(master.gain);

        baseOsc.connect(baseGain).connect(master);
        overOsc.connect(overGain).connect(master);
        master.connect(ctx.destination);
        baseOsc.start(t);
        overOsc.start(t);
        lfo.start(t);
        NS.saucer = { baseOsc, overOsc, lfo, master };
    };

    NS.stopSaucer = function () {
        if (!NS.saucer || !NS.ctx) return;
        const t = NS.ctx.currentTime;
        const { baseOsc, overOsc, lfo, master } = NS.saucer;
        master.gain.cancelScheduledValues(t);
        master.gain.setValueAtTime(master.gain.value, t);
        master.gain.linearRampToValueAtTime(0, t + 0.1);
        baseOsc.stop(t + 0.12);
        overOsc.stop(t + 0.12);
        lfo.stop(t + 0.12);
        NS.saucer = null;
    };
})();
