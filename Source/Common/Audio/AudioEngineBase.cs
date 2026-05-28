#if HAS_NAUDIO
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
#endif

namespace Arcade.Common.Audio;

// Shared plumbing for all the demos' procedural audio engines.
// - Desktop (HAS_NAUDIO): owns a single MixingSampleProvider + WaveOutEvent and
//   exposes TryPlay() so subclasses can submit their voice ISampleProviders.
// - WASM (__WASM__): exposes WasmPlay() that calls into JS interop; the
//   matching Web Audio voices live in each demo's audio.js.
// - Other TFMs: every public surface is a silent no-op.
//
// Each demo declares its own concrete `AudioEngine` static class (or instance)
// that delegates to this base for setup/teardown and provides per-game voices.
public abstract class AudioEngineBase
{
#if HAS_NAUDIO
    protected const int SampleRate = 44100;
    protected WaveOutEvent? Output;
    protected MixingSampleProvider? Mixer;
    bool _initialized;
#endif

    public void Init()
    {
#if HAS_NAUDIO
        if (_initialized) return;
        _initialized = true;
        try
        {
            Mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1));
            Mixer.ReadFully = true;
            Output = new WaveOutEvent { DesiredLatency = 60 };
            Output.Init(Mixer);
            Output.Play();
        }
        catch
        {
            // No audio device — fail silent.
            Output = null;
            Mixer = null;
        }
#endif
    }

    public void Shutdown()
    {
#if HAS_NAUDIO
        Output?.Stop();
        Output?.Dispose();
        Output = null;
        Mixer = null;
        _initialized = false;
#endif
    }

#if HAS_NAUDIO
    protected void TryPlay(ISampleProvider provider)
    {
        try { Mixer?.AddMixerInput(provider); } catch { }
    }
#endif

    // Fire-and-forget JS interop on wasm; no-op on every other TFM.
    protected static void WasmPlay(string js)
    {
#if __WASM__
        try { Uno.Foundation.WebAssemblyRuntime.InvokeJS(js); } catch { /* fail silent */ }
#endif
    }
}
