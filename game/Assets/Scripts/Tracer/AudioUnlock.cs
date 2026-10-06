using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tracer
{
    /// <summary>
    /// Browser audio rule: stays silent until the first key or pointer press,
    /// then plays a code-generated tone.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioUnlock : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [SerializeField] private float frequency = 440f;
        [SerializeField] private float duration = 0.4f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.3f;

        private AudioSource source;

        public bool Unlocked { get; private set; }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.clip = CreateTone(frequency, duration, volume);
        }

        private void Update()
        {
            if (Unlocked) return;

            bool keyPressed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
            bool pointerPressed = Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
            if (!keyPressed && !pointerPressed) return;

            Unlocked = true;
            source.Play();
        }

        private static AudioClip CreateTone(float frequency, float duration, float volume)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var data = new float[samples];
            int fade = Mathf.Min(samples / 4, SampleRate / 100); // ~10 ms fade in/out to avoid clicks
            for (int i = 0; i < samples; i++)
            {
                float envelope = 1f;
                if (fade > 0)
                {
                    if (i < fade) envelope = (float)i / fade;
                    else if (i >= samples - fade) envelope = (float)(samples - 1 - i) / fade;
                }
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / SampleRate) * volume * envelope;
            }

            AudioClip clip = AudioClip.Create("TracerTone", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
