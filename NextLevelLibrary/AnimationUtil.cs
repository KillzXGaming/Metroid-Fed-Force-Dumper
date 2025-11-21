using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelLibrary
{
    public class AnimationUtil
    {
        public static Vector3 GetValue(List<Vector3> values, float frame, float frameCount)
        {
            if (values == null || values.Count == 0 || frameCount <= 0)
                return Vector3.Zero;

            if (values.Count == 1)
                return values[0];

            frame = Math.Clamp(frame, 0, frameCount);

            float t = (frame / frameCount) * (values.Count - 1);
            t = Math.Clamp(t, 0, values.Count - 1);

            int index = (int)t;
            int nextIndex = Math.Min(index + 1, values.Count - 1);

            float localT = t - index;

            return Vector3.Lerp(values[index], values[nextIndex], localT);
        }

        public static Quaternion GetValue(List<Quaternion> values, float frame, float frameCount)
        {
            if (values == null || values.Count == 0 || frameCount <= 0)
                return Quaternion.Identity;

            if (values.Count == 1)
                return values[0];

            frame = Math.Clamp(frame, 0, frameCount);

            float t = (frame / frameCount) * (values.Count - 1);
            t = Math.Clamp(t, 0, values.Count - 1);

            int index = (int)t;
            int nextIndex = Math.Min(index + 1, values.Count - 1);

            float localT = t - index;

            return Quaternion.Slerp(values[index], values[nextIndex], localT);
        }
    }
}
