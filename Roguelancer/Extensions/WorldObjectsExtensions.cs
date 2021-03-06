﻿using Roguelancer.Models;
using Roguelancer.Settings;
/// <summary>
/// World Objects Extensions
/// </summary>
public static class WorldObjectsExtensions {
    /// <summary>
    /// Clone
    /// </summary>
    /// <param name="oldObject"></param>
    /// <returns></returns>
    public static WorldObjectsSettings Clone(this WorldObjectsSettings oldObject) {
        if (oldObject == null) throw new System.Exception("Cannot clone a null object.");
        return new WorldObjectsSettings(
                oldObject.Model.Description,
                oldObject.Model.DescriptionLong,
                oldObject.Model.StartupPosition,
                oldObject.Model.StartupRotation,
                oldObject.Model.SettingsModelObject.Clone(),
                oldObject.Model.StarSystemId,
                oldObject.Model.InitialModelUp,
                oldObject.Model.InitialModelRight,
                oldObject.Model.InitialVelocity,
                oldObject.Model.InitialCurrentThrust,
                oldObject.Model.InitialDirection,
                oldObject.Model.CargoSpace,
                oldObject.Model.ID,
                oldObject.Model.Dockable,
                oldObject.Model.DestinationIndex,
                oldObject.Model.JumpHoleTarget
            );
    }
}