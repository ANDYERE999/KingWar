using System;
using System.Collections.Generic;
using Godot;

namespace KingWar.Common
{
    public partial class AudioManager : Node
    {
        public static AudioManager Instance { get; private set; } = null!;

        private readonly Dictionary<string, AudioStreamWav> _cachedSounds = new();
        private readonly List<AudioStreamPlayer2D> _spatialPool = new();
        private readonly List<AudioStreamPlayer> _uiPool = new();

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _Ready()
        {
            GenerateAllSounds();

            // Pre-warm audio player pools
            for (int i = 0; i < 24; i++)
            {
                var p2d = new AudioStreamPlayer2D
                {
                    MaxDistance = 2500,
                    Attenuation = 1.5f
                };
                AddChild(p2d);
                _spatialPool.Add(p2d);

                var pUi = new AudioStreamPlayer();
                AddChild(pUi);
                _uiPool.Add(pUi);
            }
        }

        public void PlaySound(string soundName, float volumeDb = 0f, float pitchScale = 1.0f)
        {
            if (!_cachedSounds.TryGetValue(soundName, out var stream)) return;

            foreach (var p in _uiPool)
            {
                if (!p.Playing)
                {
                    p.Stream = stream;
                    p.VolumeDb = volumeDb;
                    p.PitchScale = pitchScale;
                    p.Play();
                    return;
                }
            }
        }

        public void PlaySpatial(string soundName, Vector2 globalPos, float volumeDb = 0f, float pitchScale = 1.0f)
        {
            if (!_cachedSounds.TryGetValue(soundName, out var stream)) return;

            foreach (var p in _spatialPool)
            {
                if (!p.Playing)
                {
                    p.GlobalPosition = globalPos;
                    p.Stream = stream;
                    p.VolumeDb = volumeDb;
                    p.PitchScale = pitchScale;
                    p.Play();
                    return;
                }
            }
        }

        private void GenerateAllSounds()
        {
            _cachedSounds["rifle_m4"] = SynthesizeRifleShot(freq: 380f, duration: 0.18f, noiseAmount: 0.65f);
            _cachedSounds["rifle_ak"] = SynthesizeRifleShot(freq: 260f, duration: 0.24f, noiseAmount: 0.75f);
            _cachedSounds["rifle_svd"] = SynthesizeRifleShot(freq: 180f, duration: 0.42f, noiseAmount: 0.85f);
            _cachedSounds["smg"] = SynthesizeRifleShot(freq: 460f, duration: 0.12f, noiseAmount: 0.55f);
            _cachedSounds["rocket_fire"] = SynthesizeRocketLaunch();
            _cachedSounds["explosion"] = SynthesizeExplosion();
            _cachedSounds["hitmarker"] = SynthesizeHitmarker();
            _cachedSounds["reload"] = SynthesizeMechanicalClick();
            _cachedSounds["heal"] = SynthesizeHealTone();
            _cachedSounds["capture_alarm"] = SynthesizeAlarmTone();
            _cachedSounds["hurt"] = SynthesizeHurtGrunt();
            _cachedSounds["deploy"] = SynthesizeDeployClick();
            _cachedSounds["grenade_bounce"] = SynthesizeBounce();
            _cachedSounds["cannon_fire"] = SynthesizeRifleShot(freq: 95f, duration: 0.65f, noiseAmount: 0.7f);
            _cachedSounds["heavy_mg"] = SynthesizeRifleShot(freq: 210f, duration: 0.19f, noiseAmount: 0.6f);
            _cachedSounds["metal_ricochet"] = SynthesizeMetalRicochet();
        }

        private static AudioStreamWav SynthesizeMetalRicochet()
        {
            int sampleRate = 22050;
            float duration = 0.25f;
            int numSamples = (int)(duration * sampleRate);
            byte[] bytes = new byte[numSamples * 2];
            var rng = new Random();

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / numSamples;
                float env = MathF.Exp(-progress * 12f);
                float sweepFreq = 2600f + MathF.Sin(progress * 25f) * 400f;
                float ping = MathF.Sin(2f * MathF.PI * sweepFreq * t) * 0.4f;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.15f;
                float sample = (ping + noise) * env;

                short pcm = (short)(Math.Clamp(sample, -1f, 1f) * 28000f);
                bytes[i * 2] = (byte)(pcm & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
            }

            return CreateWav(bytes, sampleRate);
        }

        private static AudioStreamWav CreateWav(byte[] pcmData, int sampleRate = 22050)
        {
            var wav = new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = sampleRate,
                Stereo = false,
                Data = pcmData
            };
            return wav;
        }

        private static AudioStreamWav SynthesizeRifleShot(float freq, float duration, float noiseAmount)
        {
            int sampleRate = 22050;
            int numSamples = (int)(duration * sampleRate);
            byte[] bytes = new byte[numSamples * 2];
            var rng = new Random();

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / numSamples;
                float env = MathF.Exp(-progress * 14f); // steep decay

                float osc = MathF.Sin(2f * MathF.PI * (freq * (1f - progress * 0.6f)) * t);
                float noise = ((float)rng.NextDouble() * 2f - 1f);
                float sample = (osc * (1f - noiseAmount) + noise * noiseAmount) * env;

                sample = Math.Clamp(sample, -1f, 1f);
                short pcm = (short)(sample * 30000f);
                bytes[i * 2] = (byte)(pcm & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
            }

            return CreateWav(bytes, sampleRate);
        }

        private static AudioStreamWav SynthesizeRocketLaunch()
        {
            int sampleRate = 22050;
            float duration = 0.5f;
            int numSamples = (int)(duration * sampleRate);
            byte[] bytes = new byte[numSamples * 2];
            var rng = new Random();

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / numSamples;
                float env = MathF.Sin(progress * MathF.PI) * MathF.Exp(-progress * 2.5f);

                float whoosh = ((float)rng.NextDouble() * 2f - 1f) * 0.7f;
                float tone = MathF.Sin(2f * MathF.PI * (120f + progress * 240f) * t) * 0.3f;
                float sample = (whoosh + tone) * env;

                sample = Math.Clamp(sample, -1f, 1f);
                short pcm = (short)(sample * 30000f);
                bytes[i * 2] = (byte)(pcm & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
            }

            return CreateWav(bytes, sampleRate);
        }

        private static AudioStreamWav SynthesizeExplosion()
        {
            int sampleRate = 22050;
            float duration = 0.85f;
            int numSamples = (int)(duration * sampleRate);
            byte[] bytes = new byte[numSamples * 2];
            var rng = new Random();

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / numSamples;
                float env = MathF.Exp(-progress * 4.5f);

                // Deep low rumble + noise
                float rumble = MathF.Sin(2f * MathF.PI * (55f * (1f - progress * 0.5f)) * t) * 0.6f;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.5f;
                float sample = (rumble + noise) * env;

                sample = Math.Clamp(sample, -1f, 1f);
                short pcm = (short)(sample * 32000f);
                bytes[i * 2] = (byte)(pcm & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
            }

            return CreateWav(bytes, sampleRate);
        }

        private static AudioStreamWav SynthesizeHitmarker()
        {
            int sampleRate = 22050;
            float duration = 0.05f;
            int numSamples = (int)(duration * sampleRate);
            byte[] bytes = new byte[numSamples * 2];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / numSamples;
                float env = 1f - progress;
                float sample = MathF.Sin(2f * MathF.PI * 1800f * t) * env * 0.4f;

                short pcm = (short)(sample * 30000f);
                bytes[i * 2] = (byte)(pcm & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
            }

            return CreateWav(bytes, sampleRate);
        }

        private static AudioStreamWav SynthesizeMechanicalClick()
        {
            int sampleRate = 22050;
            float duration = 0.2f;
            int numSamples = (int)(duration * sampleRate);
            byte[] bytes = new byte[numSamples * 2];
            var rng = new Random();

            for (int i = 0; i < numSamples; i++)
            {
                float progress = (float)i / numSamples;
                float env = MathF.Exp(-progress * 25f);
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.5f;
                float sample = noise * env;

                short pcm = (short)(sample * 25000f);
                bytes[i * 2] = (byte)(pcm & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
            }

            return CreateWav(bytes, sampleRate);
        }

        private static AudioStreamWav SynthesizeHealTone()
        {
            int sampleRate = 22050;
            float duration = 0.4f;
            int numSamples = (int)(duration * sampleRate);
            byte[] bytes = new byte[numSamples * 2];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / numSamples;
                float env = MathF.Sin(progress * MathF.PI);
                float sample = (MathF.Sin(2f * MathF.PI * 523.25f * t) * 0.4f +
                                MathF.Sin(2f * MathF.PI * 659.25f * t) * 0.4f) * env;

                short pcm = (short)(sample * 25000f);
                bytes[i * 2] = (byte)(pcm & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
            }

            return CreateWav(bytes, sampleRate);
        }

        private static AudioStreamWav SynthesizeAlarmTone()
        {
            int sampleRate = 22050;
            float duration = 0.6f;
            int numSamples = (int)(duration * sampleRate);
            byte[] bytes = new byte[numSamples * 2];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / numSamples;
                float pitch = 440f + MathF.Sin(progress * MathF.PI * 4f) * 120f;
                float sample = MathF.Sin(2f * MathF.PI * pitch * t) * 0.35f;

                short pcm = (short)(sample * 28000f);
                bytes[i * 2] = (byte)(pcm & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
            }

            return CreateWav(bytes, sampleRate);
        }

        private static AudioStreamWav SynthesizeHurtGrunt()
        {
            int sampleRate = 22050;
            float duration = 0.15f;
            int numSamples = (int)(duration * sampleRate);
            byte[] bytes = new byte[numSamples * 2];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / numSamples;
                float env = MathF.Exp(-progress * 8f);
                float sample = MathF.Sin(2f * MathF.PI * (180f - progress * 60f) * t) * env * 0.45f;

                short pcm = (short)(sample * 25000f);
                bytes[i * 2] = (byte)(pcm & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
            }

            return CreateWav(bytes, sampleRate);
        }

        private static AudioStreamWav SynthesizeDeployClick()
        {
            return SynthesizeMechanicalClick();
        }

        private static AudioStreamWav SynthesizeBounce()
        {
            int sampleRate = 22050;
            float duration = 0.08f;
            int numSamples = (int)(duration * sampleRate);
            byte[] bytes = new byte[numSamples * 2];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / numSamples;
                float env = MathF.Exp(-progress * 18f);
                float sample = MathF.Sin(2f * MathF.PI * 320f * t) * env * 0.4f;

                short pcm = (short)(sample * 25000f);
                bytes[i * 2] = (byte)(pcm & 0xFF);
                bytes[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
            }

            return CreateWav(bytes, sampleRate);
        }
    }
}
