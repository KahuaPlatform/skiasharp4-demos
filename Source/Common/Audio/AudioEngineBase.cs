#if HAS_NAUDIO
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
#endif

namespace Arcade.Common.Audio;

/// <summary>
/// Shared cross-platform plumbing for every demo's procedural audio engine.
/// Subclasses add per-game "voices" (short synthesized sounds) and delegate the
/// platform mechanics to this base.
/// </summary>
/// <remarks>
/// Three compilation contexts, selected by the consumer's DefineConstants:
/// <list type="bullet">
/// <item><b>Desktop (<c>HAS_NAUDIO</c>)</b> — owns one <c>MixingSampleProvider</c>
///   feeding a <c>WaveOutEvent</c> at 60 ms latency; <see cref="TryPlay"/> mixes
///   in a voice's <c>ISampleProvider</c>.</item>
/// <item><b>WASM (<c>__WASM__</c>)</b> — <see cref="WasmPlay"/> fires a JS-interop
///   call to the matching Web Audio voice in the demo's <c>audio.js</c>.</item>
/// <item><b>Any other TFM</b> — every member is a silent no-op.</item>
/// </list>
/// </remarks>
public abstract class AudioEngineBase
{
#if HAS_NAUDIO
    protected const int SampleRate = 44100;
    protected WaveOutEvent? Output;
    protected MixingSampleProvider? Mixer;
    bool _initialized;
#endif

    /// <summary>
    /// Idempotently starts the desktop audio device + mixer. No-op on WASM/other
    /// TFMs and silently disabled if no output device is available.
    /// </summary>
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

    /// <summary>
    /// Stops and disposes the desktop output device and clears the mixer so a
    /// later <see cref="Init"/> can restart cleanly. No-op off desktop.
    /// </summary>
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
    /// <summary>
    /// Adds a voice's sample provider to the running mixer (desktop only).
    /// Swallows the rare race where the mixer is being torn down concurrently.
    /// </summary>
    protected void TryPlay(ISampleProvider provider)
    {
        try { Mixer?.AddMixerInput(provider); } catch { }
    }
#endif

    /// <summary>
    /// Fire-and-forget JS-interop call to a Web Audio voice (WASM only); no-op on
    /// every other TFM. <paramref name="js"/> is a snippet like
    /// <c>globalThis.pohakuAudio.fire()</c>.
    /// </summary>
    protected static void WasmPlay(string js)
    {
#if __WASM__
        try { Uno.Foundation.WebAssemblyRuntime.InvokeJS(js); } catch { /* fail silent */ }
#endif
    }
}
