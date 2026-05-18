using UnityEngine;

public class Crosshair : MonoBehaviour
{
    public Color crosshairColor = Color.white;
    public int crosshairSize = 10;
    public int crosshairThickness = 2;
    public int crosshairGap = 4;

    void OnGUI()
    {
        int cx = Screen.width / 2;
        int cy = Screen.height / 2;
        int s = crosshairSize;
        int g = crosshairGap;
        int t = crosshairThickness;

        GUI.color = crosshairColor;

        // Horizontal
        GUI.DrawTexture(new Rect(cx - s - g, cy - t / 2, s, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx + g,     cy - t / 2, s, t), Texture2D.whiteTexture);

        // Vertikal
        GUI.DrawTexture(new Rect(cx - t / 2, cy - s - g, t, s), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - t / 2, cy + g,     t, s), Texture2D.whiteTexture);

        // Dot
        GUI.DrawTexture(new Rect(cx - t / 2, cy - t / 2, t, t), Texture2D.whiteTexture);

        GUI.color = Color.white;
    }
}