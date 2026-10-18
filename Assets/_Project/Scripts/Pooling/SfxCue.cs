using System;
using UnityEngine;

namespace PixelShooter.Pooling
{
    [Serializable]
    public class SfxCue
    {
        [SerializeField]
        private AudioClip clip;

        [SerializeField]
        [Range(0f, 1f)]
        private float volume = 1f;

        [SerializeField]
        [Min(1)]
        private int maxVoices = 4;

        [SerializeField]
        [Min(0f)]
        private float minRepeatInterval;

        [SerializeField]
        [Range(0.5f, 2f)]
        private float pitchMin = 1f;

        [SerializeField]
        [Range(0.5f, 2f)]
        private float pitchMax = 1f;

        public AudioClip Clip
        {
            get
            {
                return clip;
            }
        }

        public float Volume
        {
            get
            {
                return volume;
            }
        }

        public int MaxVoices
        {
            get
            {
                return maxVoices;
            }
        }

        public float MinRepeatInterval
        {
            get
            {
                return minRepeatInterval;
            }
        }

        public float PitchMin
        {
            get
            {
                return pitchMin;
            }
        }

        public float PitchMax
        {
            get
            {
                return pitchMax;
            }
        }
    }
}
