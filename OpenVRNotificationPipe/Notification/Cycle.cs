using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenVRNotificationPipe.Notification
{
    class Cycle
    {
        static public Func<float, float> GetFunc(bool flip, int waveType, int phaseType)
        {
            var phaseFunc = (phaseType < phaseFuncs.Length && phaseType >= 0)
                ? phaseFuncs[phaseType]
                : phaseFuncs[0];
            var waveFunc = (waveType < waveFuncs.Length && waveType >= 0)
                ? waveFuncs[waveType]
                : waveFuncs[0];
            return value => {
                var result = waveFunc(phaseFunc(value));
                return flip ? -result : result;
            };
        }

        // Phase types
        static readonly private Func<float, float> Linear = value => value;
        static readonly private Func<float, float> Sine = value => (float)Math.Sin((value * Math.PI) / 2);
        static readonly private Func<float, float> Cosine = value => (float)Math.Cos((value * Math.PI) / 2);
        static readonly private Func<float, float> NegativeSine = value => (float)-Math.Sin((value * Math.PI) / 2);
        static readonly private Func<float, float> NegativeCosine = value => (float)-Math.Cos((value * Math.PI) / 2);
        static readonly private Func<float, float>[] phaseFuncs = {
            Linear,
            Sine,
            Cosine,
            NegativeSine,
            NegativeCosine
        };

        // Wave types
        static readonly private Func<float, float> PhaseBased = value => value;
        
        // TODO: Work in progress
        static readonly private Func<float, float> Square = value => value;
        static readonly private Func<float, float> Triangular = value => value;
        static readonly private Func<float, float> Sawtooth = value => value;
        static readonly private Func<float, float> SawtoothReversed = value => value;

        static readonly private Func<float, float>[] waveFuncs = {
            PhaseBased,
            // SquareWave,
            // TriangularWave,
            // SawtoothWave,
            // SawtoothReversedWave
        };
    }

    class Cycler { 
        private float _amplitude;
        private float _frequency;
        private Func<float, float> _func;

        public Cycler() {
            Reset();
        }

        public Cycler(Payload.AnimationObject anim)
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
