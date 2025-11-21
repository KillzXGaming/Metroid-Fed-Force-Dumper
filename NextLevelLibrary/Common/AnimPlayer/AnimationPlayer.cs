using System;
using System.Collections.Generic;
using System.Text;
using Toolbox.Core;
using AvaloniaToolbox.Core.Animation;
using AvaloniaToolbox.Core;
using AvaloniaToolbox.Core.Mesh;
using System.Numerics;

namespace NextLevelLibrary
{
    public class AnimationPlayer : GenericAnimation
    {
        public GenericSkeleton Skeleton;

        /// <summary>
        /// Gets the active skeleton visbile in the scene that may be used for animation.
        /// </summary>
        /// <returns></returns>
        public GenericSkeleton GetActiveSkeleton()
        {
            foreach (var model in GenericModelCache.Models)
            {
                if (!model.IsVisible)
                    continue;

                return model.Skeleton;
            }
            return null;
        }

        public override void NextFrame()
        {
            base.NextFrame();

            bool update = false;
            var skeleton = GetActiveSkeleton();

            if (skeleton == null) return;

            var rotate_root_y_up = Quaternion.CreateFromYawPitchRoll(-1.5708F, 0, 0);

            foreach (var group in this.Groups)
            {
                if (group is AnimationGroup)
                {
                    var boneAnim = (AnimationGroup)group;
                    var bone = skeleton.FindBone(boneAnim.Name);

                    if (bone == null)
                        continue;

                    update = true;

                    Vector3 position = bone.Position;
                    Vector3 scale = bone.Scale;

                    if (boneAnim.TranslateX.HasKeys)
                        position.X = boneAnim.TranslateX.GetFrameValue(Frame);
                    if (boneAnim.TranslateY.HasKeys)
                        position.Y = boneAnim.TranslateY.GetFrameValue(Frame);
                    if (boneAnim.TranslateZ.HasKeys)
                        position.Z = boneAnim.TranslateZ.GetFrameValue(Frame);
                    /*
                    if (boneAnim.ScaleX.HasKeys)
                        scale.X = boneAnim.ScaleX.GetFrameValue(Frame);
                    if (boneAnim.ScaleY.HasKeys)
                        scale.Y = boneAnim.ScaleY.GetFrameValue(Frame);
                    if (boneAnim.ScaleZ.HasKeys)
                        scale.Z = boneAnim.ScaleZ.GetFrameValue(Frame);
                    */
                    bone.AnimationController.Position = position;
                    bone.AnimationController.Scale = scale;

                    if (boneAnim.UseQuaternion)
                    {
                        Quaternion rotation = bone.Rotation;

                        if (boneAnim.RotateX.HasKeys && boneAnim.RotateY.HasKeys &&
                            boneAnim.RotateZ.HasKeys && boneAnim.RotateW.HasKeys)
                        {
                            GenericKeyFrame[] x = boneAnim.RotateX.GetFrame(Frame);
                            GenericKeyFrame[] y = boneAnim.RotateY.GetFrame(Frame);
                            GenericKeyFrame[] z = boneAnim.RotateZ.GetFrame(Frame);
                            GenericKeyFrame[] w = boneAnim.RotateW.GetFrame(Frame);

                            Quaternion q1 = new Quaternion(x[0].Value, y[0].Value, z[0].Value, w[0].Value);
                            Quaternion q2 = new Quaternion(x[1].Value, y[1].Value, z[1].Value, w[1].Value);
                            //left key
                            if (x[0].Frame == Frame)
                                rotation = q1;
                            else if (x[1].Frame == Frame) //right key
                                rotation = q2;
                            else if (q1 !=  q2) //mixed key
                            {
                                float ratio = (Frame - x[0].Frame) / (x[1].Frame - x[0].Frame);
                                rotation = Quaternion.Slerp(q1, q2, ratio);
                            }
                            else //constant
                                rotation = q1;
                        }

                        bone.AnimationController.Rotation = rotation;
                    }
                    else
                    {
                        Vector3 rotationEuluer = bone.RotationEuler;

                        if (boneAnim.RotateX.HasKeys)
                            rotationEuluer.X = boneAnim.RotateX.GetFrameValue(Frame);
                        if (boneAnim.RotateY.HasKeys)
                            rotationEuluer.Y = boneAnim.RotateY.GetFrameValue(Frame);
                        if (boneAnim.RotateZ.HasKeys)
                            rotationEuluer.Z = boneAnim.RotateZ.GetFrameValue(Frame);

                        bone.AnimationController.EulerRotation = rotationEuluer;
                    }
                }
            }

            if (update)
                skeleton.UpdateMatrices(false);
        }
    }

    public class AnimationGroup : GenericAnimationGroup
    {
        public bool UseQuaternion { get; set; } = true;

        public AnimationTrack TranslateX = new AnimationTrack();
        public AnimationTrack TranslateY = new AnimationTrack();
        public AnimationTrack TranslateZ = new AnimationTrack();

        public AnimationTrack RotateX = new AnimationTrack();
        public AnimationTrack RotateY = new AnimationTrack();
        public AnimationTrack RotateZ = new AnimationTrack();
        public AnimationTrack RotateW = new AnimationTrack();

        public AnimationTrack ScaleX = new AnimationTrack();
        public AnimationTrack ScaleY = new AnimationTrack();
        public AnimationTrack ScaleZ = new AnimationTrack();

        public AnimationTrack TexCoordU = new AnimationTrack();
        public AnimationTrack TexCoordV = new AnimationTrack();
    }

    public class AnimationTrack : GenericAnimationTrack
    {
        public AnimationTrack() {
            this.Interpolation = InterpolationType.Linear;
        }
    }
}
