using System.Collections.Generic;
using UnityEngine;

public class EnhancedSafeAreaRegister
{
    private static EnhancedSafeAreaRegister instance;
    public static EnhancedSafeAreaRegister Instance => instance ??= new EnhancedSafeAreaRegister();

    List<EnhancedSafeArea> registeredSafeAreas = new List<EnhancedSafeArea>();

    public void RegisterSafeArea(EnhancedSafeArea safeArea)
    {
        if (safeArea == null) return;

        if (!registeredSafeAreas.Contains(safeArea))
        {
            registeredSafeAreas.Add(safeArea);
        }
    }

    public void UnregisterSafeArea(EnhancedSafeArea safeArea)
    {
        if (safeArea == null) return;

        registeredSafeAreas.Remove(safeArea);
    }

    public void UpdateBannerHeight(float bannerHeight)
    {
        foreach (var safeArea in registeredSafeAreas)
        {
            safeArea.BannerHeightPixels = bannerHeight;
        }
    }


}
