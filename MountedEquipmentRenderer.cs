using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer
{
    /// <summary>
    /// One authoritative mounted-equipment visual transform. The station and
    /// flight renderers differ only in the ship pose supplied to Build.
    /// </summary>
    public sealed class MountedEquipmentAttachment
    {
        public string HardpointId { get; init; } = string.Empty;
        public string EquipmentId { get; init; } = string.Empty;
        public EquipmentDefinition Equipment { get; init; }
        public ShipHardpoint Hardpoint { get; init; }
        public Matrix World { get; init; }
        public string VisualModelPath { get; init; } = string.Empty;

        public bool HasVisualModel => !string.IsNullOrWhiteSpace(VisualModelPath);
    }

    /// <summary>
    /// Shared cache and draw path for equipment models. Empty visual paths are
    /// intentionally supported: the authoritative mount remains valid while
    /// an equipment item without standalone art contributes no draw call.
    /// </summary>
    public static class MountedEquipmentRenderer
    {
        private static readonly Dictionary<string, Model> _modelCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _failedModelPaths = new(StringComparer.OrdinalIgnoreCase);
        private static bool _contentLoadLogged;

        public static void LoadContent(ContentManager content)
        {
            if (content == null)
            {
                return;
            }

            int attempted = 0;
            int loaded = 0;
            foreach (EquipmentDefinition equipment in EquipmentCatalog.GetAll())
            {
                if (equipment == null || string.IsNullOrWhiteSpace(equipment.VisualModelPath))
                {
                    continue;
                }

                attempted++;
                if (_modelCache.ContainsKey(equipment.VisualModelPath) || _failedModelPaths.Contains(equipment.VisualModelPath))
                {
                    if (_modelCache.ContainsKey(equipment.VisualModelPath)) loaded++;
                    continue;
                }

                try
                {
                    _modelCache[equipment.VisualModelPath] = content.Load<Model>(equipment.VisualModelPath);
                    loaded++;
                }
                catch (Exception ex)
                {
                    _failedModelPaths.Add(equipment.VisualModelPath);
                    Console.WriteLine($"[MOUNTED EQUIPMENT] Failed to load '{equipment.VisualModelPath}': {ex.Message}");
                }
            }

            if (!_contentLoadLogged)
            {
                _contentLoadLogged = true;
                Console.WriteLine($"[MOUNTED EQUIPMENT] Model cache ready: {loaded}/{attempted} visual model paths loaded; empty paths are skipped safely.");
            }
        }

        public static IReadOnlyList<MountedEquipmentAttachment> Build(
            Ship ship,
            Vector3 position,
            Matrix orientation,
            float pitchTiltAngle = 0f,
            float bankTiltAngle = 0f)
        {
            if (ship?.Loadout == null)
            {
                return Array.Empty<MountedEquipmentAttachment>();
            }

            Matrix shipPose = Ship.CreateShipPoseWorldMatrix(
                position,
                orientation,
                pitchTiltAngle,
                bankTiltAngle);

            List<MountedEquipmentAttachment> result = new();
            foreach (ShipHardpoint hardpoint in ship.Loadout.Hardpoints)
            {
                if (hardpoint == null || hardpoint.IsEmpty)
                {
                    continue;
                }

                EquipmentDefinition equipment = EquipmentCatalog.GetById(hardpoint.MountedEquipmentId);
                if (equipment == null)
                {
                    continue;
                }

                Matrix hardpointRotation = CreateRotation(hardpoint.LocalRotationDegrees);
                Matrix equipmentRotation = CreateRotation(equipment.VisualRotationDegrees);
                float scale = SanitizeScale(hardpoint.VisualScale) * SanitizeScale(equipment.VisualModelScale);
                Matrix attachmentWorld = Matrix.CreateScale(scale) * equipmentRotation * hardpointRotation *
                    Matrix.CreateTranslation(hardpoint.LocalPosition) * shipPose;

                result.Add(new MountedEquipmentAttachment
                {
                    HardpointId = hardpoint.Id ?? string.Empty,
                    EquipmentId = equipment.Id ?? hardpoint.MountedEquipmentId,
                    Equipment = equipment,
                    Hardpoint = hardpoint,
                    World = attachmentWorld,
                    VisualModelPath = equipment.VisualModelPath ?? string.Empty
                });
            }

            return result;
        }

        public static int Draw(
            Ship ship,
            Vector3 position,
            Matrix orientation,
            Matrix view,
            Matrix projection,
            Vector3 lightDirection,
            float pitchTiltAngle = 0f,
            float bankTiltAngle = 0f)
        {
            IReadOnlyList<MountedEquipmentAttachment> attachments = Build(
                ship, position, orientation, pitchTiltAngle, bankTiltAngle);
            int drawn = 0;

            foreach (MountedEquipmentAttachment attachment in attachments)
            {
                if (!attachment.HasVisualModel || !_modelCache.TryGetValue(attachment.VisualModelPath, out Model model) || model == null)
                {
                    continue;
                }

                foreach (ModelMesh mesh in model.Meshes)
                {
                    foreach (BasicEffect effect in mesh.Effects)
                    {
                        Ship.ConfigureModelEffect(
                            effect,
                            attachment.World,
                            view,
                            projection,
                            lightDirection,
                            new Vector3(0.95f, 0.72f, 0.42f),
                            new Vector3(0.52f, 0.34f, 0.18f),
                            new Vector3(0.18f, 0.15f, 0.12f));
                    }

                    mesh.Draw();
                    drawn++;
                }
            }

            return drawn;
        }

        public static string BuildDiagnostics(Ship ship, Vector3 position, Matrix orientation)
        {
            IReadOnlyList<MountedEquipmentAttachment> attachments = Build(ship, position, orientation);
            if (attachments.Count == 0)
            {
                return "Mounted equipment: none";
            }

            return "Mounted equipment: " + string.Join(" | ", attachments.Select(attachment =>
                $"{attachment.HardpointId} -> {attachment.EquipmentId}"));
        }

        private static Matrix CreateRotation(Vector3 degrees)
        {
            return Matrix.CreateFromYawPitchRoll(
                MathHelper.ToRadians(degrees.Y),
                MathHelper.ToRadians(degrees.X),
                MathHelper.ToRadians(degrees.Z));
        }

        private static float SanitizeScale(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? 1f : value;
        }
    }
}
