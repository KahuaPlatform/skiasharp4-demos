// Paku — Web Audio procedural synthesis (mirrors desktop NAudio voices).
// Loaded into globalThis by the Uno bootstrapper via EmbeddedResource.

(function () {
    let ctx = null;

    function ensureCtx() {
        if (ctx) return ctx;
        try { ctx = new AudioContext(); } catch (_) { return null; }
        // Resume on first gesture (autoplay policy)
        const resume = () => { if (ctx && ctx.state === 'suspended') ctx.resume(); };
        document.addEventListener('pointerdown', resume, { once: true });
        document.addEventListener('keydown', resume, { once: true });
        return ctx;
    }

    let thrustNode = null;
    let thrustGain = null;

    globalThis.pakuAudio = {
        playAbsorb() {
            const c = ensureCtx(); if (!c) return;
            const osc = c.createOscillator();
            const gain = c.createGain();
            osc.type = 'sine';
            osc.frequency.setValueAtTime(300, c.currentTime);
            osc.frequency.linearRampToValueAtTime(1200, c.currentTime + 0.12);
            gain.gain.setValueAtTime(0.28, c.currentTime);
            gain.gain.linearRampToValueAtTime(0, c.currentTime + 0.12);
            osc.connect(gain).connect(c.destination);
            osc.start(); osc.stop(c.currentTime + 0.13);
        },

        playDeath() {
            const c = ensureCtx(); if (!c) return;
            const osc = c.createOscillator();
            const noise = c.createBufferSource();
            const noiseGain = c.createGain();
            const oscGain = c.createGain();
            const dur = 0.6;

            // Descending saw
            osc.type = 'sawtooth';
            osc.frequency.setValueAtTime(660, c.currentTime);
            osc.frequency.exponentialRampToValueAtTime(60, c.currentTime + dur);
            oscGain.gain.setValueAtTime(0.16, c.currentTime);
            oscGain.gain.exponentialRampToValueAtTime(0.001, c.currentTime + dur);
            osc.connect(oscGain).connect(c.destination);
            osc.start(); osc.stop(c.currentTime + dur + 0.05);

            // Noise burst
            const buf = c.createBuffer(1, c.sampleRate * dur, c.sampleRate);
            const d = buf.getChannelData(0);
            for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
            noise.buffer = buf;
            noiseGain.gain.setValueAtTime(0.12, c.currentTime);
            noiseGain.gain.exponentialRampToValueAtTime(0.001, c.currentTime + dur);
            noise.connect(noiseGain).connect(c.destination);
            noise.start(); noise.stop(c.currentTime + dur + 0.05);
        },

        startThrust() {
            const c = ensureCtx(); if (!c) return;
            if (thrustNode) return;

            // Bubbly filtered noise
            const bufSize = 4096;
            thrustNode = c.createScriptProcessor(bufSize, 0, 1);
            let f1 = 0, f2 = 0;
            let phase = 0;
            thrustNode.onaudioprocess = function (ev) {
                const out = ev.outputBuffer.getChannelData(0);
                for (let i = 0; i < out.length; i++) {
                    const noise = Math.random() * 2 - 1;
                    f1 = f1 * 0.88 + noise * 0.12;
                    f2 = f2 * 0.92 + f1 * 0.08;
                    phase += 5 * 2 * Math.PI / c.sampleRate;
                    const wobble = Math.sin(phase) * 0.3;
                    out[i] = (f2 + wobble * f1) * 0.25;
                }
            };
            thrustGain = c.createGain();
            thrustGain.gain.setValueAtTime(0, c.currentTime);
            thrustGain.gain.linearRampToValueAtTime(1, c.currentTime + 0.1);
            thrustNode.connect(thrustGain).connect(c.destination);
        },

        stopThrust() {
            const c = ensureCtx();
            if (!thrustGain || !c) { thrustNode = null; thrustGain = null; return; }
            thrustGain.gain.linearRampToValueAtTime(0, c.currentTime + 0.1);
            const n = thrustNode, g = thrustGain;
            setTimeout(() => { try { n.disconnect(); g.disconnect(); } catch (_) { } }, 200);
            thrustNode = null;
            thrustGain = null;
        }
    };
})();
