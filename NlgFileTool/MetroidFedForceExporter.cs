using AvaloniaToolbox.Core;
using AvaloniaToolbox.Core.Mesh;
using IONET;
using IONET.Collada.Core.Controller;
using IONET.Core;
using IONET.Core.Model;
using IONET.Core.Skeleton;
using NextLevelLibrary;
using NextLevelLibrary.MetroidFedForce;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace NlgFileTool
{
    public class MetroidFedForceExporter
    {
        static SkeletonFormat GetSkeleton(IDataFile dataFile, ChunkFileEntry modelFile)
        {
            //Find skeleton
            SkeletonFormat skeleton = null;
            //Prepare and load skeleton first
            foreach (ChunkFileEntry f in ((ChunkTable)dataFile.Tables[0]).GetFiles)
            {
                if (f.FilePath.Value == modelFile.FilePath.Value && f.Type == ChunkType.Skeleton)
                    skeleton = new SkeletonFormat(f);
            }
            return skeleton;
        }

        static NextLevelLibrary.LM2.TextureFormatCTR GetTexture(IDataFile dataFile, uint hash)
        {
            NextLevelLibrary.LM2.TextureFormatCTR texture = null;
            foreach (ChunkFileEntry f in ((ChunkTable)dataFile.Tables[0]).GetFiles)
            {
                if (f.FilePath.Value == hash && f.Type == ChunkType.Texture)
                    texture = new NextLevelLibrary.LM2.TextureFormatCTR(f);
            }
            return texture;
        }

        public static void Export(IDataFile dataFile,
            ChunkFileEntry f, string exportFolder, string root)
        {
            var texportFolder = Path.Combine(root, "texture");
            Directory.CreateDirectory(texportFolder);

            string exportPath = Path.Combine(exportFolder, Path.GetFileName(f.FilePath.String));
            switch (f.Type)
            {
                case ChunkType.Model:
                    var model = new ModelFormat(f, GetSkeleton(dataFile, f));
                    var ioscene = ConvertModel(dataFile, model, texportFolder);
                    var rotYup = Matrix4x4.CreateRotationX(MathHelper.Deg2Rad * -90); 

                    IOManager.ExportScene(ioscene, $"{exportPath}.gltf", new ExportSettings()
                    {
                        GlobalTransform = model.SkeletonFormat == null ? Matrix4x4.Identity :
                            rotYup,
                    });
                    break;
                case ChunkType.Texture:
                    var tex = new NextLevelLibrary.LM2.TextureFormatCTR(f);
                    tex.ToGeneric().Export($"{exportPath}.png", new ImageLibrary.ExportSettings());
                    break;
                case ChunkType.Script:
                    break;
            }
        }

        static IOScene ConvertModel(IDataFile dataFile, ModelFormat modelFile, string exportFolder)
        {
            var skeletonFile = modelFile.SkeletonFormat;

            IOScene ioscene = new();

            IOModel iomodel = new();
            ioscene.Models.Add(iomodel);

            if (skeletonFile != null)
            {
                List<IOBone> iobones = new();
                foreach (var b in skeletonFile.GenericSkeleton.Bones)
                {
                    iobones.Add(new IOBone()
                    {
                        Name = b.Name,
                        Translation = b.Position,
                        Rotation = b.Rotation,
                        Scale = b.Scale,
                    });
                }
                for (int i = 0; i < iobones.Count; i++)
                {
                    var parent = skeletonFile.GenericSkeleton.Bones[i].Parent;
                    if (parent == null)
                        continue;

                    var idx = skeletonFile.GenericSkeleton.Bones.IndexOf(parent);
                    iobones[i].Parent = iobones[idx];
                }

                IOBone root = new IOBone()
                {
                    Name = "Root",
                };
                iomodel.Skeleton.RootBones.Add(root);

                for (int i = 0; i < iobones.Count; i++)
                {
                    if (iobones[i].Parent == null)
                        root.AddChild(iobones[i]);
                }
            }

            foreach (var model in modelFile.Models)
            {
                int meshIdx = 0;
                foreach (var mesh in model.Meshes)
                {
                    IOMesh iomesh = new()
                    {
                        Name = Hashing.GetString(mesh.Header.Hash),
                    };
                    iomodel.Meshes.Add(iomesh);

                    IOMaterial iomaterial = new()
                    {
                        Name = Hashing.GetString(mesh.Header.MaterialHash),
                    };
                    ioscene.Materials.Add(iomaterial);

                    if (mesh.Material.DiffuseTextureHash != 0)
                    {
                        var texture = GetTexture(dataFile, mesh.Material.DiffuseTextureHash);
                        if (texture  != null)
                        {
                            string path = Path.Combine(exportFolder,
                                Path.GetFileName(Hashing.GetString(mesh.Material.DiffuseTextureHash))) + ".png";
                            texture.ToGeneric().Export(path);

                            iomaterial.DiffuseMap = new IOTexture()
                            {
                                FilePath = path,
                                Name = path,
                            };
                        }

                     /*   string tex_name = Hashing.GetString(mesh.Material.DiffuseTextureHash);
                       */
                    }

                    foreach (var vertex in mesh.Vertices)
                    {
                        IOVertex iovertex = new();
                        iomesh.Vertices.Add(iovertex);

                        iovertex.Position = vertex.Position;
                        iovertex.Normal = System.Numerics.Vector3.Normalize(vertex.Normal);
                        iovertex.SetColor(vertex.Color.X,
                                          vertex.Color.Y, 
                                          vertex.Color.Z, 
                                          vertex.Color.W, 0);
                        iovertex.SetUV(vertex.TexCoord0.X, vertex.TexCoord0.Y, 0);
                        iovertex.SetUV(vertex.TexCoord1.X, vertex.TexCoord1.Y, 1);
                        iovertex.SetUV(vertex.TexCoord2.X, vertex.TexCoord2.Y, 2);

                        if (float.IsNaN(iovertex.Normal.X) ||
                            float.IsNaN(iovertex.Normal.Y) ||
                            float.IsNaN(iovertex.Normal.Z))
                                iovertex.Normal = new Vector3(0, 1, 0);

                        if (skeletonFile != null)
                        {
                            Matrix4x4.Invert(skeletonFile.GenericSkeleton.RootTransform, out Matrix4x4 invRoot);

                            iovertex.Position = Vector3.Transform(iovertex.Position, invRoot);
                            iovertex.Normal = Vector3.TransformNormal(iovertex.Normal, invRoot);

                            for (int j = 0; j < vertex.BoneIndices.Count; j++)
                            {
                                var boneHashIdx = vertex.BoneIndices[j];
                                var skinController = model.SkinController.MeshSkins[meshIdx];
                                if (skinController.SkinningHashes.Count <= boneHashIdx)
                                    break;

                                var boneHash = skinController.SkinningHashes[boneHashIdx];
                                //get bone from skeleton
                                var idx = skeletonFile.BoneHashToID[boneHash];
                                if (idx >= skeletonFile.GenericSkeleton.Bones.Count)
                                    break;

                                if (vertex.BoneIndices.Count == 1)
                                {
                                    iovertex.Position = System.Numerics.Vector3.Transform(
                                        iovertex.Position, skeletonFile.GenericSkeleton.Bones[idx].WorldMatrix);
                                    iovertex.Normal = System.Numerics.Vector3.TransformNormal(
                                        iovertex.Normal, skeletonFile.GenericSkeleton.Bones[idx].WorldMatrix);
                                }

                                var name = skeletonFile.GenericSkeleton.Bones[idx].Name;
                                iovertex.Envelope.Weights.Add(new IOBoneWeight()
                                {
                                    BoneName = name,
                                    Weight = vertex.BoneWeights[j],
                                });
                            }
                        }
                    }

                    IOPolygon iopoly = new();
                    iopoly.MaterialName = iomaterial.Name;
                    iomesh.Polygons.Add(iopoly);
                    foreach (var ind in mesh.Faces)
                        iopoly.Indicies.Add((int)ind);

                    meshIdx++;
                }
            }

            return ioscene;
        }
    }
}
