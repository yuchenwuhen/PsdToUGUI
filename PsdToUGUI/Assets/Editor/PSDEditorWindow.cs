﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PhotoshopFile;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(PSDEditorWindow))]
public class PSDEditorWindow : EditorWindow
{
    private static string vers = "v1.6";
    private Font font;
    private Texture2D image;
    private Vector2 scrollPos;
    private PsdFile psd;
    private int atlassize = 4096;
    private float pixelsToUnitSize = 100.0f;
    private string fileName;
    private List<string> LayerList = new List<string>();
    private bool ShowAtlas;
    private GameObject CanvasObj;
    private string PackingTag;

    #region Const

    private const string Button = "button";
    private const string Normal = "normal";
    private const string Highlight = "highlight";
    private const string Disable = "disable";
    private const string Touched = "push";

    private const int screenHeight = 720;
    private const int screenWidth = 1280;

    #endregion

    #region MenuItems

    [MenuItem("Window/uGUI/PSD Converter")]
    public static void ShowWindow()
    {
        var wnd = GetWindow<PSDEditorWindow>();

        //wnd.title = "PSD To uGUI" + vers;
        wnd.titleContent = new GUIContent("Gank PSD TO UGUI");
        wnd.minSize = new Vector2(400, 300);
        wnd.Show();
    }


    [MenuItem("Assets/Convert to uGUI", true, 20000)]
    private static bool saveLayersEnabled()
    {
        for (var i = 0; i < Selection.objects.Length; i++)
        {
            var obj = Selection.objects[i];
            var filePath = AssetDatabase.GetAssetPath(obj);
            if (filePath.EndsWith(".psd", StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [MenuItem("Assets/Convert to uGUI", false, 20000)]
    private static void saveLayers()
    {
        var obj = Selection.objects[0];

        var window = GetWindow<PSDEditorWindow>(true, "Gank PSD TO UGUI");
        //var window = EditorWindow.GetWindow<PSDEditorWindow> (true, "PSD to uGUI " + vers);
        window.minSize = new Vector2(400, 300);
        window.image = (Texture2D) obj;
        window.LoadInformation(window.image);
        window.Show();
    }

    #endregion

    public void OnGUI()
    {
        font = (Font) EditorGUILayout.ObjectField("Font", font, typeof (Font), true);
        EditorGUI.BeginChangeCheck();

        image = (Texture2D) EditorGUILayout.ObjectField("PSD File", image, typeof (Texture2D), true);

        var changed = EditorGUI.EndChangeCheck();

        if (image != null)
        {
            if (changed)
            {
                var path = AssetDatabase.GetAssetPath(image);

                if (path.ToUpper().EndsWith(".PSD", StringComparison.CurrentCultureIgnoreCase))
                {
                    LoadInformation(image);
                }
                else
                {
                    psd = null;
                }
            }
            if (font == null)
            {
                EditorGUILayout.HelpBox("请选择PSD中Label所使用的字体", MessageType.Error);
            }
            if (psd != null)
            {
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

                foreach (var layer in psd.Layers)
                {
                    var sectionInfo = (LayerSectionInfo) layer.AdditionalInfo
                        .SingleOrDefault(x => x is LayerSectionInfo);
                    if (sectionInfo == null)
                    {
                        layer.Visible = EditorGUILayout.ToggleLeft(layer.Name, layer.Visible);
                    }
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("全选", GUILayout.Width(200)))
                {
                    foreach (var layer in psd.Layers)
                    {
                        //保证序列中不存在多个元素
                        var sectionInfo = (LayerSectionInfo) layer.AdditionalInfo
                            .SingleOrDefault(x => x is LayerSectionInfo);
                        if (sectionInfo == null)
                        {
                            layer.Visible = true;
                        }
                    }
                }
                if (GUILayout.Button("全不选", GUILayout.Width(200)))
                {
                    foreach (var layer in psd.Layers)
                    {
                        var sectionInfo = (LayerSectionInfo) layer.AdditionalInfo
                            .SingleOrDefault(x => x is LayerSectionInfo);
                        if (sectionInfo == null)
                        {
                            layer.Visible = false;
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
//				GUILayout.Label ("Packing Tag");
//                PackingTag = EditorGUILayout.TextArea(image.name);

                PackingTag = image.name;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndScrollView();


//				if (GUILayout.Button ("Create atlas && GUI ")) {
//				
//					ShowAtlas = !ShowAtlas;
//				}
                if (ShowAtlas)
                {
                    atlassize = EditorGUILayout.IntField("Max. atlas size", atlassize);

                    if (!((atlassize != 0) && ((atlassize & (atlassize - 1)) == 0)))
                    {
                        EditorGUILayout.HelpBox("Atlas size should be a power of 2", MessageType.Warning);
                    }

                    pixelsToUnitSize = EditorGUILayout.FloatField("Pixels To Unit Size", pixelsToUnitSize);

                    if (pixelsToUnitSize <= 0)
                    {
                        EditorGUILayout.HelpBox("Pixels To Unit Size should be greater than 0.", MessageType.Warning);
                    }
                    if (GUILayout.Button("Start"))
                    {
                        CreateAtlas();
                    }
                }

                if (GUILayout.Button("开始处理"))
                {
                    ExportLayers();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("This texture is not a PSD file.", MessageType.Error);
            }
        }
    }

    private Texture2D CreateTexture(Layer layer)
    {
        if ((int) layer.Rect.width == 0 || (int) layer.Rect.height == 0)
        {
            return null;
        }

        var tex = new Texture2D((int) layer.Rect.width, (int) layer.Rect.height, TextureFormat.RGBA32, true);
        var pixels = new Color32[tex.width*tex.height];
        var red = (from l in layer.Channels
            where l.ID == 0
            select l).First();
        var green = (from l in layer.Channels
            where l.ID == 1
            select l).First();
        var blue = (from l in layer.Channels
            where l.ID == 2
            select l).First();
        var alpha = layer.AlphaChannel;
        for (var i = 0; i < pixels.Length; i++)
        {
            var r = red.ImageData[i];
            var g = green.ImageData[i];
            var b = blue.ImageData[i];
            byte a = 255;
            if (alpha != null)
            {
                a = alpha.ImageData[i];
            }
            var mod = i%tex.width;
            var n = ((tex.width - mod - 1) + i) - mod;
            pixels[pixels.Length - n - 1] = new Color32(r, g, b, a);
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private void CreateAtlas()
    {
        var textures = new List<Texture2D>();
        var spriteRenderers = new List<SpriteRenderer>();
        LayerList = new List<string>();
        var zOrder = 0;
        var root = new GameObject(fileName);
        foreach (var layer in psd.Layers)
        {
            if (layer.Visible && layer.Rect.width > 0 && layer.Rect.height > 0)
            {
                if (LayerList.IndexOf(layer.Name.Split('|').Last()) == -1)
                {
                    LayerList.Add(layer.Name.Split('|').Last());
                    var tex = CreateTexture(layer);
                    textures.Add(tex);
                    var go = new GameObject(layer.Name);
                    var sr = go.AddComponent<SpriteRenderer>();
                    go.transform.position = new Vector3((layer.Rect.width/2 + layer.Rect.x)/pixelsToUnitSize,
                        (-layer.Rect.height/2 - layer.Rect.y)/pixelsToUnitSize, 0);
                    spriteRenderers.Add(sr);
                    sr.sortingOrder = zOrder++;
                    go.transform.parent = root.transform;
                }
            }
        }
        Rect[] rects;
        var atlas = new Texture2D(atlassize, atlassize);
        var textureArray = textures.ToArray();
        rects = atlas.PackTextures(textureArray, 2, atlassize);
        var Sprites = new List<SpriteMetaData>();
        for (var i = 0; i < rects.Length; i++)
        {
            var smd = new SpriteMetaData();
            smd.name = spriteRenderers[i].name.Split('|').Last();
            smd.rect = new Rect(rects[i].xMin*atlas.width,
                rects[i].yMin*atlas.height,
                rects[i].width*atlas.width,
                rects[i].height*atlas.height);
            smd.pivot = new Vector2(0.5f, 0.5f); // Center is default otherwise layers will be misaligned
            smd.alignment = (int) SpriteAlignment.Center;
            Sprites.Add(smd);
        }

        // Need to load the image first
        var assetPath = AssetDatabase.GetAssetPath(image);
        var path = Path.Combine(Path.GetDirectoryName(assetPath),
            Path.GetFileNameWithoutExtension(assetPath) + "_atlas" + ".png");

        var buf = atlas.EncodeToPNG();
        File.WriteAllBytes(path, buf);
        AssetDatabase.Refresh();
        // Get our texture that we loaded
        atlas = (Texture2D) AssetDatabase.LoadAssetAtPath(path, typeof (Texture2D));
        var textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
        // Make sure the size is the same as our atlas then create the spritesheet
        textureImporter.maxTextureSize = atlassize;
        textureImporter.spritesheet = Sprites.ToArray();
        textureImporter.textureType = TextureImporterType.Sprite;
        textureImporter.spriteImportMode = SpriteImportMode.Multiple;
        textureImporter.spritePivot = new Vector2(0.5f, 0.5f);
        textureImporter.spritePixelsPerUnit = pixelsToUnitSize;
        textureImporter.textureFormat = TextureImporterFormat.AutomaticTruecolor;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
        foreach (var tex in textureArray)
        {
            DestroyImmediate(tex);
        }
        AssetDatabase.Refresh();
        DestroyImmediate(root);
        var atlases = AssetDatabase.LoadAllAssetsAtPath(path).Select(x => x as Sprite).Where(x => x != null).ToArray();
        CreateGUI(atlases);
    }

    private void CreateGUI(Sprite[] atlas)
    {
        for (var i = 0; i < psd.Layers.Count; i++)
        {
            if (((LayerSectionInfo) psd.Layers[i].AdditionalInfo
                .SingleOrDefault(x => x is LayerSectionInfo) == null) && (psd.Layers[i].Visible))
            {
                var RealLayerName = psd.Layers[i].Name.Split('|').Last();

                var textlayer = (LayerTextInfo) psd.Layers[i].AdditionalInfo.SingleOrDefault(x => x is LayerTextInfo);

                if (RealLayerName.StartsWith(Button) && RealLayerName.EndsWith(Touched))
                {
                    var temp = GameObject.Find(RealLayerName.Split('_')[0] + "_" + RealLayerName.Split('_')[1]);
                    var State = temp.GetComponent<Selectable>().spriteState;
                    State.pressedSprite = atlas[i];
                    temp.GetComponent<Selectable>().spriteState = State;
                }
                else if (RealLayerName.StartsWith(Button) && RealLayerName.EndsWith(Highlight))
                {
                    var temp = GameObject.Find(RealLayerName.Split('_')[0] + "_" + RealLayerName.Split('_')[1]);
                    var State = temp.GetComponent<Selectable>().spriteState;
                    State.highlightedSprite = atlas[i];
                    temp.GetComponent<Selectable>().spriteState = State;
                }
                else if (RealLayerName.StartsWith(Button) && RealLayerName.EndsWith(Disable))
                {
                    var instant = CreatePanel(psd.Layers[i].Name.Split('|'));

                    string tempName = RealLayerName.Split('_')[0] + "_" + RealLayerName.Split('_')[1];

                    var temp = GameObject.Find(tempName);
                    var State = temp.GetComponent<Selectable>().spriteState;
                    State.disabledSprite = atlas[ Array.FindIndex(atlas, x => x.name == RealLayerName)] ;
                    temp.GetComponent<Selectable>().spriteState = State;

                    if (textlayer != null)
                    {
                        instant.AddComponent<Text>();
                        instant.GetComponent<Text>().text = textlayer.text;
                        instant.GetComponent<Text>().color = ColorPicker(psd.Layers[i]);
                        instant.GetComponent<Text>().font = font;
                        instant.GetComponent<RectTransform>().sizeDelta =
                            new Vector2(atlas[Array.FindIndex(atlas, x => x.name == RealLayerName)].rect.width,
                                atlas[Array.FindIndex(atlas, x => x.name == RealLayerName)].rect.height);
                        instant.GetComponent<Text>().resizeTextForBestFit = true;
                    }
                    else
                    {
                        instant.AddComponent<Image>();
                        instant.GetComponent<Image>().sprite =
                            atlas[Array.FindIndex(atlas, x => x.name == RealLayerName)];
                        instant.GetComponent<Image>().SetNativeSize();
                    }

                    instant.SetActive(true);
                    instant.GetComponent<RectTransform>().anchorMax = Vector2.zero;
                    instant.GetComponent<RectTransform>().anchorMin = Vector2.zero;
                    instant.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                    instant.GetComponent<RectTransform>().pivot = Vector2.zero;
                    var tempParent = instant.transform.parent;

                    instant.transform.position = new Vector3(psd.Layers[i].Rect.xMin,
                        CanvasObj.GetComponent<RectTransform>().rect.height / 2 - psd.Layers[i].Rect.yMax, 0);
                    instant.transform.GetComponent<RectTransform>().SetParent(tempParent);
                }
                else
                {
                    var instant = CreatePanel(psd.Layers[i].Name.Split('|'));
                    instant.name = psd.Layers[i].Name.Split('|').Last();
                    instant.SetActive(false);

                    if (textlayer != null)
                    {
                        instant.AddComponent<Text>();
                        instant.GetComponent<Text>().text = textlayer.text;
                        instant.GetComponent<Text>().color = ColorPicker(psd.Layers[i]);
                        instant.GetComponent<Text>().font = font;
                        instant.GetComponent<RectTransform>().sizeDelta =
                            new Vector2(atlas[Array.FindIndex(atlas, x => x.name == RealLayerName)].rect.width,
                                atlas[Array.FindIndex(atlas, x => x.name == RealLayerName)].rect.height);
                        instant.GetComponent<Text>().resizeTextForBestFit = true;
                    }
                    else
                    {
                        instant.AddComponent<Image>();
                        instant.GetComponent<Image>().sprite =
                            atlas[Array.FindIndex(atlas, x => x.name == RealLayerName)];
                        instant.GetComponent<Image>().SetNativeSize();
                    }

                    instant.SetActive(true);
                    instant.GetComponent<RectTransform>().anchorMax = Vector2.zero;
                    instant.GetComponent<RectTransform>().anchorMin = Vector2.zero;
                    instant.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                    instant.GetComponent<RectTransform>().pivot = Vector2.zero;
                    var temp = instant.transform.parent;

                    float canvasHeight = screenHeight; // CanvasObj.GetComponent<RectTransform>().rect.height;
                    float psdyMax = psd.Layers[i].Rect.yMax;

                    UnityEngine.Debug.Log("Canvas height " + canvasHeight);

                    instant.GetComponent<RectTransform>().position = new Vector3(psd.Layers[i].Rect.xMin,
                        canvasHeight - psdyMax,
                        0);

//                    instant.transform.position = new Vector3(psd.Layers[i].Rect.xMin,
//                        CanvasObj.GetComponent<RectTransform>().rect.height/2 - psd.Layers[i].Rect.yMax, 0);

                    instant.transform.GetComponent<RectTransform>().SetParent(temp);

//                    if (RealLayerName.StartsWith(Button))
//                    {
//                        instant.name = RealLayerName.Split('_')[0] + "_" + RealLayerName.Split('_')[1];
//                        instant.AddComponent<Button>().transition = Selectable.Transition.SpriteSwap;
//                    }
                }
            }
        }

//        Transform topLayer = CanvasObj.transform.GetChild(0);
//
//        for (int i = 0; i < topLayer.childCount; i++)
//        {
//            Transform child = topLayer.GetChild(i);
//
//            child.parent = null;
//
//            child.parent = CanvasObj.transform;
//        }

    }

    private GameObject CreatePanel(string[] path)
    {
        var pathtemp = new List<string>();

        pathtemp.Add("Canvas");
        pathtemp.AddRange(path);

        CanvasObj = GameObject.Find(image.name);

        var PathObj = new List<GameObject>();

        if (CanvasObj == null)
        {
            CanvasObj = new GameObject();
            CanvasObj.name = image.name;
            Canvas canvas = CanvasObj.AddComponent<Canvas>();

            canvas.GetComponent<RectTransform>().position = Vector3.zero;

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = CanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            //canvas.GetComponent<RectTransform>().anchoredPosition = new Vector2(667,375);
            //canvas.GetComponent<RectTransform>().anchoredPosition3D = new Vector3(667, 375, 0);

            UnityEngine.Debug.Log("Created new Canvas");
        }

        PathObj.Add(CanvasObj);

        for (var i = 1; i < pathtemp.Count - 1; i++)
        {
            if (PathObj[i - 1].transform.Find(pathtemp[i]) == null)
            {
                var temp = new GameObject();
                temp.SetActive(false);
                temp.AddComponent<RectTransform>().position = new Vector3(0, 0, 0);
                temp.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 0);
                temp.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0.5f);
                temp.name = pathtemp[i];
                temp.transform.GetComponent<RectTransform>().SetParent(PathObj[i - 1].transform);
                
                PathObj.Add(temp);

                if (temp.name.StartsWith(Button) && !temp.name.EndsWith(Normal) &&
                    !temp.name.EndsWith(Highlight) && !temp.name.EndsWith(Disable))
                {
                    temp.AddComponent<Button>().transition = Selectable.Transition.SpriteSwap;
                }

                temp.SetActive(true);
            }
            else
            {
                PathObj.Add(PathObj[i - 1].transform.Find(pathtemp[i]).gameObject);
            }

        }

        var temp1 = new GameObject();
        temp1.SetActive(false);
        temp1.AddComponent<RectTransform>().position = new Vector3(0, 0, 0);
        temp1.name = pathtemp[pathtemp.Count - 1];

        temp1.transform.GetComponent<RectTransform>().SetParent(PathObj[pathtemp.Count - 2].transform);
        PathObj.Add(temp1);
        temp1.SetActive(true);

        return PathObj.Last();
    }

    public static void ApplyLayerSections(List<Layer> layers)
    {
        var stack = new Stack<string>();


        foreach (var layer in Enumerable.Reverse(layers))
        {
            var sectionInfo = (LayerSectionInfo) layer.AdditionalInfo
                .SingleOrDefault(x => x is LayerSectionInfo);
            if (sectionInfo == null)
            {
                var Reverstack = stack.ToArray();
                Array.Reverse(Reverstack);
                layer.Name = string.Join("|", Reverstack) + "|" + layer.Name;
            }
            else
            {
                switch (sectionInfo.SectionType)
                {
                    case LayerSectionType.OpenFolder:


                        stack.Push(layer.Name);
                        break;
                    case LayerSectionType.Layer:
                        stack.Push(layer.Name);
                        break;
                    case LayerSectionType.ClosedFolder:

                        stack.Push(layer.Name);

                        break;
                    case LayerSectionType.SectionDivider:


                        stack.Pop();
                        break;
                }
            }
        }
    }


    private void ExportLayers()
    {
        LayerList = new List<string>();
        var atlas = new List<Sprite>();

        var path = AssetDatabase.GetAssetPath(image).Split('.')[0];

        Directory.CreateDirectory(path);
        foreach (var layer in psd.Layers)
        {
            if (layer.Visible && layer.Rect.width > 0 && layer.Rect.height > 0)
            {
                if (LayerList.IndexOf(layer.Name.Split('|').Last()) == -1)
                {
                    LayerList.Add(layer.Name.Split('|').Last());
                    var tex = CreateTexture(layer);
                    if (tex == null)
                    {
                        continue;
                    }
                    atlas.Add(SaveAsset(tex, layer.Name.Split('|').Last()));
                    DestroyImmediate(tex);
                }
            }
        }
        CreateGUI(atlas.ToArray());
    }

    private Sprite SaveAsset(Texture2D tex, string syffux)
    {
        var path = AssetDatabase.GetAssetPath(image).Split('.')[0] + "/" + syffux + ".png";

        var buf = tex.EncodeToPNG();
        File.WriteAllBytes(path, buf);
        AssetDatabase.Refresh();
        // Load the texture so we can change the type
        AssetDatabase.LoadAssetAtPath(path, typeof (Texture2D));
        var textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;

        textureImporter.textureType = TextureImporterType.Sprite;
        textureImporter.spriteImportMode = SpriteImportMode.Single;
        textureImporter.maxTextureSize = atlassize;
        if (!string.IsNullOrEmpty(PackingTag))
        {
            textureImporter.spritePackingTag = PackingTag;
        }

        textureImporter.textureFormat = TextureImporterFormat.AutomaticTruecolor;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return (Sprite) AssetDatabase.LoadAssetAtPath(path, typeof (Sprite));
    }

    public void LoadInformation(Texture2D Img)
    {
        var path = AssetDatabase.GetAssetPath(Img);

        psd = new PsdFile(path, Encoding.Default);
        fileName = Path.GetFileNameWithoutExtension(path);
        ApplyLayerSections(psd.Layers);
    }

    public Color32 ColorPicker(Layer layer)
    {
        var pixels = new Color32[(int) layer.Rect.width*(int) layer.Rect.height];
        var red = (from l in layer.Channels
            where l.ID == 0
            select l).First();
        var green = (from l in layer.Channels
            where l.ID == 1
            select l).First();
        var blue = (from l in layer.Channels
            where l.ID == 2
            select l).First();
        var alpha = layer.AlphaChannel;
        for (var i = 0; i < pixels.Length; i++)
        {
            var r = red.ImageData[i];
            var g = green.ImageData[i];
            var b = blue.ImageData[i];
            byte a = 255;
            if (alpha != null)
            {
                a = alpha.ImageData[i];
            }
            var mod = i%(int) layer.Rect.width;
            var n = (((int) layer.Rect.width - mod - 1) + i) - mod;
            pixels[pixels.Length - n - 1] = new Color32(r, g, b, a);
        }
        var r1 = 0;
        var g1 = 0;
        var b1 = 0;
        byte a1 = 255;
        pixels.ToList().ForEach(delegate(Color32 name)
        {
            r1 += name.r;
            g1 += name.g;
            b1 += name.b;
        }
            );
        return new Color32((byte) (r1/pixels.Count()), (byte) (g1/pixels.Count()), (byte) (b1/pixels.Count()), a1);
    }
}


