using System;
namespace Frociart {
    internal static class GridSnap {
        internal const float Step=24;
        // Pointer transforms can put an exact half-cell a few float ulps below
        // the midpoint. The tolerance is less than 0.00003 diagram units.
        internal static float Round(float value){return (float)(Math.Sign(value)*Math.Floor(Math.Abs(value)/(double)Step+0.500001)*Step);}
    }
}
