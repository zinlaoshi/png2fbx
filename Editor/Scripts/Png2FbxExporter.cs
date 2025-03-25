using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace Zin.Png2Fbx.Editor
{
    public static class Png2FbxExporter
    {
        const string MenuAssetRootPath = "Assets/Zin/";
        const string MenuGameObjectRootPath = "GameObject/Zin/";
        [MenuItem(MenuAssetRootPath + nameof(ExportSubSprites), true)]
        private static bool CanExportSubSprites() => Selection.activeObject is Sprite;

        [MenuItem(MenuAssetRootPath + nameof(ExportSubSprites))]
        public static void ExportSubSprites()
        {
            var folder = EditorUtility.OpenFolderPanel("Export folder", "", "");

            foreach (var obj in Selection.objects)
            {
                var sprite = obj as Sprite;
                if (sprite == null)
                    continue;

                var extracted = CreateTextureFromSprite(sprite);

                SavePNG(extracted, folder);
            }

        }

        // Since a sprite may exist anywhere on a tex2d, this will crop out the sprite's claimed region and return a new, cropped, tex2d.
        private static Texture2D CreateTextureFromSprite(Sprite sprite)
        {
            var texture = sprite.texture;

            // Create a temporary RenderTexture of the same size as the texture
            RenderTexture tmp = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);

            // Blit the pixels on texture to the RenderTexture
            Graphics.Blit(texture, tmp);

            // Backup the currently set RenderTexture
            RenderTexture previous = RenderTexture.active;

            // Set the current RenderTexture to the temporary one we created
            RenderTexture.active = tmp;

            // Create a new readable Texture2D to copy the pixels to it
            Texture2D myTexture2D = new Texture2D(texture.width, texture.height);

            // Copy the pixels from the RenderTexture to the new Texture
            myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            myTexture2D.Apply();

            // Reset the active RenderTexture
            RenderTexture.active = previous;

            // Release the temporary RenderTexture
            RenderTexture.ReleaseTemporary(tmp);

            // "myTexture2D" now has the same pixels from "texture" and it's re
            var output = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height);
            var r = sprite.textureRect;
            //var pixels = myTexture2D.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
            var pixels = myTexture2D.GetPixels((int)r.x, (int)r.y, (int)sprite.rect.width, (int)sprite.rect.height);

            output.SetPixels(pixels);
            output.Apply();
            output.name = myTexture2D.name + sprite.name;

            return output;
        }

        private static string SavePNG(Texture2D tex, string saveToDirectory)
        {
            if (!System.IO.Directory.Exists(saveToDirectory))
            {
                System.IO.Directory.CreateDirectory(saveToDirectory);
            }

            var filePath = System.IO.Path.Combine(saveToDirectory, tex.name + ".png");
            System.IO.File.WriteAllBytes(filePath, tex.EncodeToPNG());

            return filePath;
        }

        [MenuItem(MenuAssetRootPath + nameof(CreateMaterialsFromPng), true)]
        private static bool CanCreateMaterialsFromPng() => Selection.activeObject is Texture2D;
        [MenuItem(MenuAssetRootPath + nameof(CreateMaterialsFromPng))]
        private static void CreateMaterialsFromPng()
        {
            var folder = EditorUtility.OpenFolderPanel("Export folder", "", "");

            foreach (Object o in Selection.objects)
            {
                if (o.GetType() != typeof(Texture2D))
                {
                    Debug.LogError("This isn't a texture: " + o);
                    continue;
                }

                SaveMaterialFromPNG(o as Texture2D, ConvertRelativePath(folder));
            }
        }
        static string SaveMaterialFromPNG(Texture2D selected, string saveToDirectory)
        {
            var material = new Material(Shader.Find("Unlit/Texture"));
            material.mainTexture = selected;

            var newAssetName = System.IO.Path.Combine(saveToDirectory, selected.name + ".mat");
            AssetDatabase.CreateAsset(material, newAssetName);
            AssetDatabase.SaveAssets();

            return newAssetName;
        }

        [MenuItem(MenuAssetRootPath + nameof(CreateQuadFBXFromMaterials), true)]
        private static bool CanCreateQuadFBXFromMaterials() => Selection.activeObject is Material;
        [MenuItem(MenuAssetRootPath + nameof(CreateQuadFBXFromMaterials))]
        private static void CreateQuadFBXFromMaterials()
        {
            var folder = EditorUtility.OpenFolderPanel("Export folder", "", "");

            foreach (Object o in Selection.objects)
            {
                if (o.GetType() != typeof(Material))
                {
                    Debug.LogError("This isn't a Material: " + o);
                    continue;
                }

                SaveQuadFBXFromMateral(o as Material, ConvertRelativePath(folder));
            }
        }
        static void SaveQuadFBXFromMateral(Material selected, string saveToDirectory)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.GetComponent<Renderer>().material = selected;
            quad.name = selected.name;

            UnityEditor.Formats.Fbx.Exporter.ModelExporter.ExportObject(System.IO.Path.Combine(saveToDirectory, selected.name + ".fbx"), quad);

            GameObject.DestroyImmediate(quad);
        }
        [MenuItem(MenuAssetRootPath + nameof(CreateQuadFBXFromSubSprites), true)]
        private static bool CanCreateQuadFBXFromSubSprites()
        {
            return Selection.activeObject is Sprite;
        }

        [MenuItem(MenuAssetRootPath + nameof(CreateQuadFBXFromSubSprites))]
        private static void CreateQuadFBXFromSubSprites()
        {
            var folder = EditorUtility.OpenFolderPanel("Export folder", "", "");
            var relativeFolder = ConvertRelativePath(folder);
            var materialFolder = System.IO.Path.Combine(relativeFolder, "materials");
            if (!System.IO.Directory.Exists(materialFolder))
                System.IO.Directory.CreateDirectory(materialFolder);

            var fbxFolder = System.IO.Path.Combine(relativeFolder, "fbx");
            if (!System.IO.Directory.Exists(fbxFolder))
                System.IO.Directory.CreateDirectory(fbxFolder);

            foreach (var obj in Selection.objects)
            {
                var sprite = obj as Sprite;
                if (sprite == null)
                    continue;

                var extracted = CreateTextureFromSprite(sprite);
                SavePNG(extracted, folder);
                AssetDatabase.Refresh();

                var selectedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(System.IO.Path.Combine(relativeFolder, extracted.name + ".png"));
                var materialPath = SaveMaterialFromPNG(selectedTexture, materialFolder);
                AssetDatabase.Refresh();

                var selectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                SaveQuadFBXFromMateral(selectedMaterial, fbxFolder);
            }
        }
        [MenuItem(MenuGameObjectRootPath + nameof(ConvertQuadFromGrid), true)]
        private static bool CanCreateQuadFBXFromGrid()
        {
            if (Selection.activeObject is GameObject)
            {
                return (Selection.activeObject as GameObject).TryGetComponent<Grid>(out var comp);
            }

            return false;
        }
        
        [MenuItem(MenuGameObjectRootPath + nameof(ConvertQuadFromGrid))]
        private static void ConvertQuadFromGrid()
        {
            var colorSpace = PlayerSettings.colorSpace;
            if (colorSpace != ColorSpace.Gamma)
                PlayerSettings.colorSpace = ColorSpace.Gamma;

            var folder = EditorUtility.OpenFolderPanel("Export folder", "", "");
            var relativeFolder = ConvertRelativePath(folder);
            var materialFolder = System.IO.Path.Combine(relativeFolder, "materials");
            if (!System.IO.Directory.Exists(materialFolder))
                System.IO.Directory.CreateDirectory(materialFolder);

            var fbxFolder = System.IO.Path.Combine(relativeFolder, "fbx");
            if (!System.IO.Directory.Exists(fbxFolder))
                System.IO.Directory.CreateDirectory(fbxFolder);

            var activeGameObject = Selection.activeObject as GameObject;
            var convertRootObject = new GameObject("__Converted__");
            GameObject.DestroyImmediate(activeGameObject.transform.Find(convertRootObject.name)?.gameObject);            
            convertRootObject.transform.SetParent(activeGameObject.transform, false);

            Dictionary<Sprite, string> assetPathList = new Dictionary<Sprite, string>();

            foreach (var obj in Selection.objects)
            {
                if (!(obj as GameObject).TryGetComponent<Grid>(out var gridComp))
                    continue;

                var tilemapList = gridComp.GetComponentsInChildren<Tilemap>();
                foreach (var tilemap in tilemapList)
                {
                    var tilemapObj = new GameObject(tilemap.name);
                    tilemapObj.transform.SetParent(convertRootObject.transform);
                    tilemapObj.transform.localPosition = tilemap.transform.localPosition + Vector3.back * tilemap.GetComponent<TilemapRenderer>().sortingOrder;
                    for (int y = tilemap.cellBounds.y; y < tilemap.cellBounds.size.y; y++)
                    {
                        for (int x = tilemap.cellBounds.x; x < tilemap.cellBounds.size.x; x++)
                        {
                            var cellPosition = new Vector3Int(x, y);
                            var sprite = tilemap.GetSprite(cellPosition);
                            if (sprite == null)
                                continue;
                            
                            var quad = CreateQuadFromSprite(assetPathList, tilemapObj.transform, tilemap.CellToLocal(cellPosition) - tilemap.GetTransformMatrix(cellPosition).GetPosition(), tilemap.GetTransformMatrix(cellPosition).rotation, sprite, folder, relativeFolder, materialFolder);
                            Debug.Log($"{cellPosition} {quad.name} {tilemap.GetTransformMatrix(cellPosition).GetPosition()}");
                        }
                    }

                    CreateQuadTilemapChildren(tilemap.transform, assetPathList, tilemapObj.transform, folder, relativeFolder, materialFolder, 0);
                }
            }

            PlayerSettings.colorSpace = colorSpace;
            //UnityEditor.Formats.Fbx.Exporter.ModelExporter.ExportObject(System.IO.Path.Combine(fbxFolder, activeGameObject.name + ".fbx"), activeGameObject);
        }

        static void CreateQuadTilemapChildren(Transform target, Dictionary<Sprite, string> assetPathList, Transform parent, string folder, string relativeFolder, string materialFolder, int depth)
        {
            var offsetPosition = (depth == 0 ? new Vector3(-0.5f, -0.5f) : Vector3.zero);
            for (int i = 0; i < target.childCount; i++)
            {
                var child = target.GetChild(i);                

                var spr = child.GetComponent<SpriteRenderer>();
                if (spr == null || spr.sprite == null)
                {
                    var newObj = new GameObject(child.name);
                    newObj.transform.SetParent(parent.transform);
                    newObj.transform.localPosition = child.localPosition + offsetPosition;

                    CreateQuadTilemapChildren(child, assetPathList, newObj.transform, folder, relativeFolder, materialFolder, depth + 1);
                }
                else
                {
                    var quad = CreateQuadFromSprite(assetPathList, parent, child.localPosition + offsetPosition + Vector3.back * spr.sortingOrder, Quaternion.identity, spr.sprite, folder, relativeFolder, materialFolder);
                    quad.name = child.name;

                    CreateQuadTilemapChildren(child, assetPathList, quad.transform, folder, relativeFolder, materialFolder, depth + 1);
                }                
            }
        }

        static GameObject CreateQuadFromSprite(Dictionary<Sprite, string> assetPathList, Transform convertRootObject, Vector3 localPosition, Quaternion localRotation, Sprite sprite, string folder, string relativeFolder, string materialFolder)
        {
            if (!assetPathList.ContainsKey(sprite))
            {
                var extracted = CreateTextureFromSprite(sprite);
                SavePNG(extracted, folder);
                AssetDatabase.Refresh();

                var selectedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(System.IO.Path.Combine(relativeFolder, extracted.name + ".png"));
                var materialPath = SaveMaterialFromPNG(selectedTexture, materialFolder);
                AssetDatabase.Refresh();

                assetPathList.Add(sprite, materialPath);
            }

            var selectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPathList[sprite]);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.GetComponent<Renderer>().material = selectedMaterial;
            quad.name = selectedMaterial.name;

            quad.transform.SetParent(convertRootObject.transform, false);
            quad.transform.localPosition = Vector3.zero;

            var pivot = new Vector2((sprite.rect.width- sprite.pivot.x) / sprite.rect.width, (sprite.rect.height - sprite.pivot.y) / sprite.rect.height) - 0.5f * Vector2.one;
            
            quad.transform.localScale = new Vector3(sprite.rect.width / sprite.pixelsPerUnit, sprite.rect.height / sprite.pixelsPerUnit, 1);
            var pivot2 = pivot.x * quad.transform.localScale.x * Vector3.right + pivot.y * quad.transform.localScale.y * Vector3.up;            
            quad.transform.localPosition = localPosition + pivot2;
            quad.transform.RotateAround(quad.transform.position - pivot2, Vector3.forward, localRotation.eulerAngles.z);
            return quad;
        }
        static string ConvertRelativePath(string absolutePath)
        {
            var rootAssetIndex = absolutePath.IndexOf("/Assets") + 1;
            return absolutePath.Substring(rootAssetIndex, absolutePath.Length - rootAssetIndex);
        }
    }
}