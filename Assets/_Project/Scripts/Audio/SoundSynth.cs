using System;
using UnityEngine;

/// <summary>
/// Builds the game's sound effects and music loop in code (no audio files): short envelopes over sine,
/// square and noise oscillators. Cheap to generate once at startup.
/// </summary>
public static class SoundSynth
{
    public const int RATE = 22050;

    private static readonly System.Random Noise = new(1234);

    public static AudioClip Make(string name, float seconds, Func<float, float> sample)
    {
        int count = Mathf.CeilToInt(seconds * RATE);
        var data = new float[count];
        for (int i = 0; i < count; i++)
            data[i] = Mathf.Clamp(sample(i / (float)RATE), -1f, 1f);
        var clip = AudioClip.Create(name, count, 1, RATE, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static float Sine(float t, float f) => Mathf.Sin(2f * Mathf.PI * f * t);
    public static float Square(float t, float f) => Sine(t, f) >= 0f ? 1f : -1f;
    public static float Tri(float t, float f) => Mathf.PingPong(t * f * 2f, 1f) * 2f - 1f;
    public static float White() => (float)(Noise.NextDouble() * 2.0 - 1.0);
    /// <summary>Fast attack, exponential decay.</summary>
    public static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);
    public static float Note(int semitonesFromA4) => 440f * Mathf.Pow(2f, semitonesFromA4 / 12f);

    public static AudioClip Click() => Make("click", 0.06f, t => Sine(t, 1400f) * Env(t, 0.002f, 0.012f) * 0.5f);
    public static AudioClip Tick() => Make("tick", 0.15f, t => (Sine(t, 880f) * 0.7f + Sine(t, 1760f) * 0.2f) * Env(t, 0.003f, 0.05f) * 0.6f);
    public static AudioClip Go() => Make("go", 0.5f, t => (Square(t, 1046f) * 0.25f + Sine(t, 1568f) * 0.4f) * Env(t, 0.005f, 0.18f) * 0.55f);

    public static AudioClip Boom()
    {
        float low = 0f;
        return Make("boom", 1.3f, t =>
        {
            low += (White() - low) * 0.08f;
            float thump = Sine(t, Mathf.Lerp(90f, 35f, Mathf.Clamp01(t * 3f))) * Env(t, 0.004f, 0.25f);
            return (low * 2.4f * Env(t, 0.002f, 0.35f) + thump) * 0.8f;
        });
    }

    public static AudioClip Bang()
    {
        float low = 0f;
        return Make("bang", 0.6f, t =>
        {
            low += (White() - low) * 0.35f;
            return (low * Env(t, 0.001f, 0.06f) * 1.6f + Sine(t, 120f) * Env(t, 0.002f, 0.09f)) * 0.8f;
        });
    }

    public static AudioClip Blank() => Make("blank", 0.25f, t =>
        (t < 0.02f ? Square(t, 2400f) * 0.35f : 0f) + (t > 0.11f && t < 0.125f ? Square(t, 1800f) * 0.3f : 0f));

    public static AudioClip Safe() => Make("safe", 0.45f, t =>
        Sine(t, t < 0.12f ? Note(15) : Note(19)) * Env(t < 0.12f ? t : t - 0.12f, 0.004f, 0.12f) * 0.5f);

    public static AudioClip Nope() => Make("nope", 0.4f, t => Square(t, t < 0.18f ? 180f : 140f) * Env(t, 0.005f, 0.2f) * 0.25f);

    public static AudioClip Escape() => Arpeggio("escape", new[] { 3, 7, 10, 15 }, 0.09f, 0.6f);
    public static AudioClip RoundOver() => Arpeggio("roundover", new[] { 10, 7, 3, 10 }, 0.14f, 0.5f);
    public static AudioClip Fanfare() => Arpeggio("fanfare", new[] { 3, 7, 10, 15, 10, 15, 19 }, 0.12f, 0.55f);

    public static AudioClip Pop() => Make("pop", 0.12f, t => Sine(t, Mathf.Lerp(300f, 900f, t * 8f)) * Env(t, 0.002f, 0.04f) * 0.5f);
    public static AudioClip Crack()
    {
        return Make("crack", 0.35f, t => White() * Env(t, 0.001f, 0.07f) * (Mathf.Sin(t * 180f) > 0.2f ? 0.6f : 0.15f));
    }
    public static AudioClip Whoosh() => Make("whoosh", 0.6f, t => Sine(t, Mathf.Lerp(700f, 120f, t / 0.6f)) * Env(t, 0.02f, 0.25f) * 0.35f);

    private static AudioClip Arpeggio(string name, int[] notes, float step, float volume)
    {
        float length = notes.Length * step + 0.4f;
        return Make(name, length, t =>
        {
            int i = Mathf.Min(notes.Length - 1, (int)(t / step));
            float local = t - i * step;
            float decay = i == notes.Length - 1 ? 0.3f : 0.09f;
            float f = Note(notes[i]);
            return (Square(local, f) * 0.25f + Sine(local, f * 2f) * 0.35f) * Env(local, 0.004f, decay) * volume;
        });
    }

    /// <summary>Upbeat 8-bar loop: kick, hats, bass and a plucky arpeggio over C - Am - F - G.</summary>
    public static AudioClip MusicLoop()
    {
        const float bpm = 124f;
        float beat = 60f / bpm;
        float bar = beat * 4f;
        int[][] chords = { new[] { -9, -5, -2 }, new[] { -12, -9, -5 }, new[] { -16, -12, -9 }, new[] { -14, -10, -7 } };
        int[] roots = { -33, -36, -40, -38 };
        float lengthSeconds = bar * 8f;
        return Make("music", lengthSeconds, t =>
        {
            int barIndex = (int)(t / bar) % 8;
            int chord = barIndex / 2 % 4;
            float inBeat = t % beat;
            float inEighth = t % (beat / 2f);
            int eighth = (int)(t / (beat / 2f)) % 8;

            float kick = Sine(inBeat, Mathf.Lerp(120f, 45f, Mathf.Clamp01(inBeat * 12f))) * Env(inBeat, 0.002f, 0.09f) * 0.55f;
            float hat = (eighth % 2 == 1 ? White() * Env(inEighth, 0.001f, 0.015f) * 0.12f : 0f);
            float bass = Tri(t, Note(roots[chord] + (eighth % 4 == 3 ? 12 : 0))) * Env(inEighth, 0.004f, 0.12f) * 0.32f;
            int arpNote = chords[chord][eighth % 3] + 12;
            float lead = (Square(inEighth, Note(arpNote)) * 0.06f + Sine(inEighth, Note(arpNote)) * 0.12f) * Env(inEighth, 0.003f, 0.08f);
            return (kick + hat + bass + lead) * 0.8f;
        });
    }
}
