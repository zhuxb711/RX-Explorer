﻿namespace ShareClassLibrary
{
    public enum CreateType
    {
        File,
        Folder
    }

    public enum CollisionOptions
    {
        None,
        RenameOnCollision,
        OverrideOnCollision
    }

    public enum ModifyAttributeAction
    {
        Add,
        Remove
    }

    public enum WindowState
    {
        Normal = 0,
        Minimized = 1,
        Maximized = 2
    }
}
