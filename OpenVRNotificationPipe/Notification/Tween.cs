using System;
using System.Diagnostics;

namespace OpenVRNotificationPipe.Notification
{
    class Tween
    {
        static public Func<float, float> GetFunc(int index)
        {
            var func = (index < interpolatorFuncs.Length && index >= 0)
                ? interpolatorFuncs[index]
                : interpolatorFuncs[0];
            return func;
        }

        static readonly private Func<float, float> Linear = value => value;
        static readonly private Func<float, float> Sine = value => (float) Math.Sin((value * Math.PI) / 2);
        static readonly private Func<float, float> Quadratic = value => RaiseToPowerTween(value, 2);
        static readonly private Func<float, float> Cubic = value => RaiseToPowerTween(value, 3);
        static readonly private Func<float, float> Quartic = value => RaiseToPowerTween(value, 4);
        static readonly private Func<float, float> Quintic = value => RaiseToPowerTween(value, 5);
        static readonly private Func<float, float> Circle = value => (float) Math.Sqrt(1 - Math.Pow(value - 1, 2));
        static readonly private Func<float, float> Back = value => BackTween(value);
        static readonly private Func<float, float> Elastic = value => ElasticTween(value);
        static readonly private Func<float, float> Bounce = value => BounceTween(value);

        static readonly private Func<float, float>[] interpolatorFuncs = {
            Linear,
            Sine,
            Quadratic,
            Cubic,
            Quartic,
            Quintic,
            Circle,
            Back,
            Elastic,
            Bounce
        };

        static private float RaiseToPowerTween(float value, int power) {
            return 1f - (float) Math.Pow(1f - value, power);
        }

        static private float BounceTween(float value)
        {
            var n1 = 7.5625f;
            var d1 = 2.75f;
            if (value < 1 / d1) return n1 * value * value;
            else if (value < 2 / d1) return n1 * (value -= 1.5f / d1) * value + 0.75f;
            else if (value < 2.5 / d1) return n1 * (value -= 2.25f / d1) * value + 0.9375f;
            else return n1 * (value -= 2.625f / d1) * value + 0.984375f;
        }

        static private float ElasticTween(float value)
        {
            var c4 = (float) (2 * Math.PI) / 3f;
            return value == 0 ? 0
                 : value == 1 ? 1
                 : (float) Math.Pow(2, -10 * value) * (float) Math.Sin((value * 10 - 0.75) * c4) + 1;
        }

        static private float BackTween(float value) {
            var c1 = 1.70158f;
            var c3 = c1 + 1;
            return 1f + c3 * (float) Math.Pow(value - 1, 3) + c1 * (float) Math.Pow(value - 1, 2);
        }
    }
}
