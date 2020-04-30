using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class Test
{
    [MenuItem("MtTool/Test")]
    public static void TestCanvas()
    {
        var CanvasObj = new GameObject();
        CanvasObj.name = "cc";
        Canvas canvas = CanvasObj.AddComponent<Canvas>();

        canvas.GetComponent<RectTransform>().position = Vector3.zero;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = CanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        //canvas.GetComponent<RectTransform>().anchoredPosition = new Vector2(667, 375);
        //canvas.GetComponent<RectTransform>().anchoredPosition3D = new Vector3(667, 375, 0);

        UnityEngine.Debug.Log("Created new Canvas");
    }
}
