using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenVRNotificationPipe.Notification
{
    class Cycle
    {
        static public Func<float, float> GetFunc(int waveType, int phaseType)
        {
            var phaseFunc = (phaseType < phaseFuncs.Length && phaseType >= 0)
                ? phaseFuncs[phaseType]
                : phaseFuncs[0];
            var waveFunc = (waveType < waveFuncs.Length && waveType >= 0)
                ? waveFuncs[waveType]
                : waveFuncs[0];
            return value => {
                return waveFunc(phaseFunc(value));
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
        static readonly private Func<float, float> None = value => 1f;
        static readonly private Func<float, float> PhaseBased = value => value;
        
        // TODO: Work in progress
        static readonly private Func<float, float> Square = value => value;
        static readonly private Func<float, float> Triangular = value => value;
        static readonly private Func<float, float> Sawtooth = value => value;
        static readonly private Func<float, float> SawtoothReversed = value => value;

        static readonly private Func<float, float>[] waveFuncs = {
            None,
            PhaseBased,
            // SquareWave,
            // TriangularWave,
            // SawtoothWave,
            // SawtoothReversedWave
        };
    }

    class Cycler { 
        private float _amplitude = 0;
        private float _frequency = 0;
        private Func<float, float> _func = Cycle.GetFunc(0, 0);

        public Cycler() {
            // Empty constructor if no config was provided, uses above defaults.
        }

        public Cycler(Payload.Animation anim)
        {
            _amplitude = anim.amplitude;
            _frequency = anim.frequency;
            _func = Cycle.GetFunc(anim.waveType, anim.phaseType);
        }

        public Cycler(float amplitude, float frequency, int phaseType, int waveType) {
            _amplitude = amplitude;
            _frequency = frequency;
            _func = Cycle.GetFunc(waveType, phaseType);
        }

        public float GetRatio(float value) {
            return _func(value*_frequency)*_amplitude;
        }

        public void Reset() {
            _amplitude = 0;
            _frequency = 0;
            _func = Cycle.GetFunc(0, 0);
        }
    }
}
