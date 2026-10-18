using PixelShooter.UI;
using UnityEngine;

public class UIHome : UIPage
{
    public void OnSetting()
    {
        if (uiManager == null)
            return;

        uiManager.ShowSettings();
    }
}
