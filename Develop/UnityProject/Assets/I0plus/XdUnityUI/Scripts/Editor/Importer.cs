﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_2019_1_OR_NEWER
using UnityEditor.U2D;
using UnityEngine.U2D;
#endif

namespace XdUnityUI.Editor
{
    /// <summary>
    ///     based on Baum2/Editor/Scripts/BaumImporter file.
    /// </summary>
    public sealed class updateDisplayProgressBar : AssetPostprocessor
    {
        private static int progressTotal = 1;
        private static int progressCount;
        private static bool _autoEnableFlag; // デフォルトがチェック済みの時には true にする

        public override int GetPostprocessOrder()
        {
            return 1000;
        }

        private static void UpdateDisplayProgressBar(string message = "")
        {
            if (progressTotal > 1)
                EditorUtility.DisplayProgressBar("XdUnitUI Import",
                    $"{progressCount}/{progressTotal} {message}",
                    (float) progressCount / progressTotal);
        }

        /// <summary>
        ///     自動インポート
        /// </summary>
        /// <param name="importedAssets"></param>
        /// <param name="deletedAssets"></param>
        /// <param name="movedAssets"></param>
        /// <param name="movedFromAssetPaths"></param>
        public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
        }

        [MenuItem("Assets/XdUnityUI/Import Selected Folders")]
        public static async Task MenuImportFromSelectFolder()
        {
            var folderPaths = ProjectHighlightedFolders();
            await ImportFolders(folderPaths, true, false);
        }

        [MenuItem("Assets/XdUnityUI/Import Selected Folders", true)]
        public static bool MenuImportSelectedFolderCheck()
        {
            var folderPaths = ProjectHighlightedFolders();
            return folderPaths.Any();
        }

        [MenuItem("Assets/XdUnityUI/Import Selected Folders(Layout Only)")]
        public static async Task MenuImportSelectedFolderLayoutOnly()
        {
            var folderPaths = ProjectHighlightedFolders();
            await ImportFolders(folderPaths, false, false);
        }

        [MenuItem("Assets/XdUnityUI/Import Selected Folders(Layout Only)", true)]
        public static bool MenuImportSelectedFolderLayoutOnlyCheck()
        {
            var folderPaths = ProjectHighlightedFolders();
            return folderPaths.Any();
        }


        [MenuItem("Assets/XdUnityUI/Specify Folder Import...")]
        public static async Task MenuImportSpecifiedFolder()
        {
            var path = EditorUtility.OpenFolderPanel("Specify Exported Folder", "", "");
            if (string.IsNullOrWhiteSpace(path)) return;

            var folders = new List<string> {path};
            await ImportFolders(folders, true, false);
        }

        [MenuItem("Assets/XdUnityUI/Specify Folder Import(layout only)...")]
        public static async Task MenuImportSpecifiedFolderLayoutOnly()
        {
            var path = EditorUtility.OpenFolderPanel("Specify Exported Folder", "", "");
            if (string.IsNullOrWhiteSpace(path)) return;

            var folders = new List<string> {path};
            await ImportFolders(folders, false, false);
        }

        /// <summary>
        ///     Project ウィンドウで、ハイライトされているディレクトリを取得する
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<string> ProjectHighlightedFolders()
        {
            var folders = new List<string>();

            foreach (var obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) folders.Add(path);
            }

            return folders;
        }


        private static async Task ImportFolders(IEnumerable<string> importFolderPaths, bool convertImageFlag,
            bool deleteAssetsFlag)
        {
            var importedAssets = new List<string>();

            foreach (var importFolderPath in importFolderPaths)
            {
                // ディレクトリの追加
                importedAssets.Add(importFolderPath);

                // ファイルの追加
                var files = Directory.EnumerateFiles(
                    importFolderPath, "*", SearchOption.TopDirectoryOnly);

                foreach (var file in files)
                {
                    var extension = Path.GetExtension(file).ToLower();
                    if (extension == ".meta" || !convertImageFlag && extension == ".png") continue;
                    importedAssets.Add(file);
                }
            }

            await Import(importedAssets);
        }

        private static bool IsFolder(string path)
        {
            try
            {
                return File.GetAttributes(path).HasFlag(FileAttributes.Directory);
            }
            catch (Exception exception)
            {
                // ignored
                Debug.LogError(exception.Message);
            }

            return false;
        }

        private static void DeleteEntry(string path)
        {
            File.Delete(path);
        }


        /// <summary>
        ///     Assetディレクトリに追加されたファイルを確認、インポート処理を行う
        /// </summary>
        /// <param name="importedPaths"></param>
        /// <param name="movedAssets"></param>
        /// <param name="deleteImportEntriesFlag"></param>
        private static async Task Import(IReadOnlyCollection<string> importedPaths)
        {
            progressTotal = importedPaths.Count;
            if (progressTotal == 0) return;
            progressCount = 0;

            var changed = false;

            // スプライト出力フォルダの作成
            foreach (var importedPath in importedPaths)
            {
                if (!IsFolder(importedPath)) continue;
                // ディレクトリであった場合
                var spriteOutputPath = EditorUtil.GetBaumSpritesFullPath(importedPath);
                var importedFullPath = Path.GetFullPath(importedPath);
                if (Directory.Exists(spriteOutputPath))
                {
                    // すでにあるフォルダ　インポートファイルと比較して、出力先にある必要のないファイルを削除する
                    // ダブっている分は比較し、異なっている場合に上書きするようにする
                    var outputFolderInfo = new DirectoryInfo(spriteOutputPath);
                    var importFolderInfo = new DirectoryInfo(importedFullPath);

                    var outputFolderSpriteList = outputFolderInfo.GetFiles("*.png", SearchOption.AllDirectories);
                    var importFolderSpriteList = importFolderInfo.GetFiles("*.png", SearchOption.AllDirectories);

                    // outputフォルダにある importにはないファイルをリストアップする
                    var deleteEntries = outputFolderSpriteList.Except(importFolderSpriteList, new FileInfoComparer());
                    // outputフォルダ内
                    foreach (var fileInfo in deleteEntries)
                    {
                        var deleteFileName = fileInfo.FullName;
                        DeleteEntry(fileInfo.FullName);
                        DeleteEntry(deleteFileName + ".meta");
                        changed = true;
                    }
                }
                else
                {
                    var folderName = Path.GetFileName(Path.GetFileName(importedPath));
                    var folderPath = EditorUtil.GetOutputSpritesFolderPath();
                    AssetDatabase.CreateFolder(EditorUtil.ToUnityPath(folderPath),
                        folderName);
                    changed = true;
                }
            }

            if (changed)
            {
                // ディレクトリが作成されたり、画像が削除されるためRefresh
                AssetDatabase.Refresh();
                changed = false;
            }

            // フォルダが作成され、そこに画像を出力する場合
            // Refresh後、DelayCallで画像生成することで、処理が安定した
            await Task.Delay(1000);

            // SpriteイメージのハッシュMapをクリアしたかどうかのフラグ
            // importedAssetsに一気に全部の新規ファイルが入ってくる前提の処理
            // 全スライス処理が走る前、最初にClearImageMapをする
            var clearedImageMap = false;
            // 画像コンバート　スライス処理
            var messageCounter = new Dictionary<string, int>();
            var total = 0;
            foreach (var importedAsset in importedPaths)
            {
                if (!importedAsset.EndsWith(".png", StringComparison.Ordinal)) continue;
                //
                if (!clearedImageMap)
                {
                    TextureUtil.ClearImageMap();
                    clearedImageMap = true;
                }

                // スライス処理
                var message = TextureUtil.SliceSprite(importedAsset);
                changed = true;

                total++;
                progressCount += 1;
                UpdateDisplayProgressBar(message);

                // 出力されたログをカウントする
                if (messageCounter.ContainsKey(message))
                    messageCounter[message] = messageCounter[message] + 1;
                else
                    messageCounter.Add(message, 1);
            }

            foreach (var keyValuePair in messageCounter)
                Debug.Log($"[XdUnityUI] {keyValuePair.Key} {keyValuePair.Value}/{total}");

            if (changed)
            {
                AssetDatabase.Refresh();
                changed = false;
            }

            await Task.Delay(1000);

            // Create Prefab
            foreach (var assetPath in importedPaths)
            {
                UpdateDisplayProgressBar("layout");
                progressCount += 1;
                if (!assetPath.EndsWith(".layout.json", StringComparison.Ordinal)) continue;

                var folderName = Path.GetFileName(assetPath).Replace(".layout.json", "");
                var spriteRootPath =
                    EditorUtil.ToUnityPath(Path.Combine(EditorUtil.GetOutputSpritesFolderPath(), folderName));
                var fontRootPath = EditorUtil.ToUnityPath(EditorUtil.GetFontsPath());
                var creator = new PrefabCreator(spriteRootPath, fontRootPath, assetPath);
                var go = creator.Create();
                var savePath =
                    EditorUtil.ToUnityPath(Path.Combine(EditorUtil.GetOutputPrefabsFolderPath(),
                        folderName + ".prefab"));
                try
                {
#if UNITY_2018_3_OR_NEWER
                    var savedAsset = PrefabUtility.SaveAsPrefabAsset(go, savePath);
                    Debug.Log("[XdUnityUI] Created prefab: " + savePath, savedAsset);
#else
                    Object originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(savePath);
                    if (originalPrefab == null) originalPrefab = PrefabUtility.CreateEmptyPrefab(savePath);
                    PrefabUtility.ReplacePrefab(go, originalPrefab, ReplacePrefabOptions.ReplaceNameBased);
#endif
                }
                catch
                {
                    // 変換中例外が起きた場合もテンポラリGameObjectを削除する
                    Object.DestroyImmediate(go);
                    EditorUtility.ClearProgressBar();
                    throw;
                }

                // 作成に成功した
                Object.DestroyImmediate(go);
            }

            EditorUtility.ClearProgressBar();
        }

        private static void CreateSpritesFolder(string asset)
        {
            var directoryName = Path.GetFileName(Path.GetFileName(asset));
            var directoryPath = EditorUtil.GetOutputSpritesFolderPath();
            var directoryFullPath = Path.Combine(directoryPath, directoryName);
            if (Directory.Exists(directoryFullPath))
                // 画像出力用フォルダに画像がのこっていればすべて削除
                // Debug.LogFormat("[XdUnityUI] Delete Exist Sprites: {0}", EditorUtil.ToUnityPath(directoryFullPath));
                foreach (var filePath in Directory.GetFiles(directoryFullPath, "*.png",
                    SearchOption.TopDirectoryOnly))
                    File.Delete(filePath);
            else
                // Debug.LogFormat("[XdUnityUI] Create Directory: {0}", EditorUtil.ToUnityPath(directoryPath) + "/" + directoryName);
                AssetDatabase.CreateFolder(EditorUtil.ToUnityPath(directoryPath),
                    Path.GetFileName(directoryFullPath));
        }

        /**
        * SliceSpriteではつかなくなったが､CreateAtlasでは使用する
        */
        private static string ImportSpritePathToOutputPath(string asset)
        {
            var folderName = Path.GetFileName(Path.GetDirectoryName(asset));
            var folderPath = Path.Combine(EditorUtil.GetOutputSpritesFolderPath(), folderName);
            var fileName = Path.GetFileName(asset);
            return Path.Combine(folderPath, fileName);
        }

#if UNITY_2019_1_OR_NEWER
        private static void CreateAtlas(string name, List<string> importPaths)
        {
            var filename = Path.Combine(EditorUtil.GetBaumAtlasPath(), name + ".spriteatlas");

            var atlas = new SpriteAtlas();
            var settings = new SpriteAtlasPackingSettings
            {
                padding = 8,
                enableTightPacking = false
            };
            atlas.SetPackingSettings(settings);
            var textureSettings = new SpriteAtlasTextureSettings
            {
                filterMode = FilterMode.Point,
                generateMipMaps = false,
                sRGB = true
            };
            atlas.SetTextureSettings(textureSettings);

            var textureImporterPlatformSettings = new TextureImporterPlatformSettings {maxTextureSize = 8192};
            atlas.SetPlatformSettings(textureImporterPlatformSettings);

            // iOS用テクスチャ設定
            // ASTCに固定してしまいっている　これらの設定を記述できるようにしたい
            textureImporterPlatformSettings.overridden = true;
            textureImporterPlatformSettings.name = "iPhone";
            textureImporterPlatformSettings.format = TextureImporterFormat.ASTC_4x4;
            atlas.SetPlatformSettings(textureImporterPlatformSettings);

            // アセットの生成
            AssetDatabase.CreateAsset(atlas, EditorUtil.ToUnityPath(filename));

            // ディレクトリを登録する場合
            // var iconsDirectory = AssetDatabase.LoadAssetAtPath<Object>("Assets/ExternalAssets/Baum2/CreatedSprites/UIESMessenger");
            // atlas.Add(new Object[]{iconsDirectory});
        }
#endif

        private class FileInfoComparer : IEqualityComparer<FileInfo>
        {
            public bool Equals(FileInfo iLhs, FileInfo iRhs)
            {
                if (iLhs.Name == iRhs.Name) return true;

                return false;
            }

            public int GetHashCode(FileInfo fi)
            {
                var s = fi.Name;
                return s.GetHashCode();
            }
        }
    }
}