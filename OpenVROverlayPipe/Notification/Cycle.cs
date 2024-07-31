using System;
using OpenVROverlayPipe.Input;

namespace OpenVROverlayPipe.Notification
{
    internal static class Cycle
    {
        public static Func<float, float> GetFunc(bool flip, AnimationWaveformEnum waveType, AnimationPhaseEnum phaseType)
        {
            var phaseFunc = GetPhaseFunc(phaseType);
            var waveFunc = GetWaveformFunc(waveType);
            return value => {
                var result = waveFunc(phaseFunc(value));
                return flip ? -result : result;
            };
        }

        // region Phase types
        private static readonly Func<float, float> Linear = value => value;
        private static readonly Func<float, float> Sine = value => (float)Math.Sin((value * Math.PI) / 2);
        private static readonly Func<float, float> Cosine = value => (float)Math.Cos((value * Math.PI) / 2);
        private static readonly Func<float, float> NegativeSine = value => (float)-Math.Sin((value * Math.PI) / 2);
        private static readonly Func<float, float> NegativeCosine = value => (float)-Math.Cos((value * Math.PI) / 2);

        private static Func<float,float> GetPhaseFunc(AnimationPhaseEnum type)
        {
            return type switch
            {
                AnimationPhaseEnum.Sine => Sine,
                AnimationPhaseEnum.Cosine => Cosine,
                AnimationPhaseEnum.NegativeSine => NegativeSine,
                AnimationPhaseEnum.NegativeCosine => NegativeCosine,
                _ => Linear
            };
        }
        // endregion

        // region Wave types
        private static readonly Func<float, float> PhaseBased = value => value;
        
        // TODO: Work in progress
        private static readonly Func<float, float> Square = value => value;
        private static readonly Func<float, float> Triangular = value => value;
        private static readonly Func<float, float> Sawtooth = value => value;
        private static readonly Func<float, float> SawtoothReversed = value => value;

        private static Func<float, float> GetWaveformFunc(AnimationWaveformEnum type)
        {
            return type switch
            {
                AnimationWaveformEnum.PhaseBased => PhaseBased,
                _ => PhaseBased
            };
        }
        // endregion
    }

    class Cycler { 
        private float _amplitude;
        private float _frequency;
        private Func<float, float> _func;

        public Cycler() {
            Reset();
        }

        public Cycler(DataOverlay.AnimationObject anim)
        {
            _amplitude = anim.Amplitude;
            _frequency = anim.Frequency;
            _func = Cycle.GetFunc(anim.FlipWaveform, anim.Waveform, anim.Phase);
        }

        public float GetRatio(float value) {
            return _func(value*_frequency)*_amplitude;
        }

        public void Reset() {
            _amplitude = 0;
            _frequency = 0;
            _func = Cycle.GetFunc(false, 0, 0);
        }
    }
}
