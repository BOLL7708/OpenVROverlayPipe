using System;
using System.Diagnostics;

namespace OpenVRNotificationPipe.Notification
{
    class Interpolator
    {
        static public Func<float, float> GetFunc(int index)
        {
            var func = (index < interpolatorFuncs.Length && index >= 0)
                ? interpolatorFuncs[index]
                : interpolatorFuncs[0];
            return func;
        }

        static readonly internal Func<float, float> Linear = value => value;
        static readonly internal Func<float, float> Pow2 = value => (float)Math.Pow(value, 2);
        static readonly internal Func<float, float> Pow3 = value => (float)Math.Pow(value, 3);
        static readonly internal Func<float, float> Pow4 = value => (float)Math.Pow(value, 4);
        static readonly internal Func<float, float> Pow5 = value => (float)Math.Pow(value, 5);

        static readonly internal Func<float, float>[] interpolatorFuncs = {
            Linear,
            Pow2,
            Pow3,
            Pow4,
            Pow5
        };
    }
}
