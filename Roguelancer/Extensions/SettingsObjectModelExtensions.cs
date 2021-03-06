﻿using Roguelancer.Models;
/// <summary>
/// Settings Object Model Extensions
/// </summary>
public static class SettingsObjectModelExtensions {
    /// <summary>
    /// Clone
    /// </summary>
    /// <param name="oldObject"></param>
    /// <returns></returns>
    public static SettingsObjectModel Clone(this SettingsObjectModel oldObject) {
        if (oldObject == null) throw new System.Exception("Cannot clone a null object.");
        return new SettingsObjectModel(
            oldObject.ModelPath,
            oldObject.ModelType,
            oldObject.Enabled,
            oldObject.ModelId,
            oldObject.Scaling
        );
    }
}