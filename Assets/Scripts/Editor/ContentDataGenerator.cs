using UnityEditor;
using UnityEngine;
using FactoryDelivery.Data;
using System.IO;

namespace FactoryDelivery.Editor
{
    /// <summary>
    /// 기획서에 명시된 자원, 시설, 레시피 데이터를 자동으로 생성하는 에디터 스크립트입니다.
    /// </summary>
    public class ContentDataGenerator : EditorWindow
    {
        private const string RootPath = "Assets/Data";

        [MenuItem("FactoryDelivery/Generate Content Data")]
        public static void ShowWindow()
        {
            GetWindow<ContentDataGenerator>("Content Generator");
        }

        private void OnGUI()
        {
            if (GUILayout.Button("기획서 기반 모든 데이터 생성 (Resources, Recipes)"))
            {
                GenerateAllContent();
            }
        }

        private void GenerateAllContent()
        {
            EnsureDirectories();

            // 1. 농산 및 식량류
            var rice = CreateResource("쌀", ResourceCategory.Agricultural, 1.0f, 10, true);
            var riceBag = CreateResource("쌀 가마니", ResourceCategory.Agricultural, 1.2f, 25, false);
            var makgeolli = CreateResource("관영 막걸리", ResourceCategory.Agricultural, 0.8f, 60, false);
            CreateRecipe(rice, riceBag, 5f, 1);
            CreateRecipe(riceBag, makgeolli, 8f, 2);

            var bean = CreateResource("콩", ResourceCategory.Agricultural, 1.0f, 10, true);
            var meju = CreateResource("메주", ResourceCategory.Agricultural, 1.1f, 25, false);
            var soySauce = CreateResource("관영 간장", ResourceCategory.Agricultural, 0.9f, 60, false);
            CreateRecipe(bean, meju, 5f, 1);
            CreateRecipe(meju, soySauce, 8f, 2);

            // 2. 직물 및 의복류
            var cotton = CreateResource("목화", ResourceCategory.Textile, 0.9f, 15, true);
            var cottonCloth = CreateResource("무명천", ResourceCategory.Textile, 1.1f, 40, false);
            var militaryUniform = CreateResource("군졸 군복", ResourceCategory.Textile, 1.3f, 100, false);
            CreateRecipe(cotton, cottonCloth, 10f, 1);
            CreateRecipe(cottonCloth, militaryUniform, 15f, 2);

            var cocoon = CreateResource("누에고치", ResourceCategory.Textile, 0.8f, 20, true);
            var silkThread = CreateResource("명주실", ResourceCategory.Textile, 0.9f, 50, false);
            var highSilk = CreateResource("최고급 비단", ResourceCategory.Textile, 1.1f, 150, false);
            CreateRecipe(cocoon, silkThread, 12f, 1);
            CreateRecipe(silkThread, highSilk, 18f, 2);

            // 3. 광물 및 자재류 (무거움)
            var ironOre = CreateResource("철광석", ResourceCategory.Mineral, 1.8f, 20, true);
            var ironIngot = CreateResource("무쇠 주괴", ResourceCategory.Mineral, 2.0f, 60, false);
            var musket = CreateResource("조총", ResourceCategory.Mineral, 1.5f, 200, false);
            CreateRecipe(ironOre, ironIngot, 20f, 1);
            CreateRecipe(ironIngot, musket, 30f, 2);

            var log = CreateResource("통나무", ResourceCategory.Mineral, 1.6f, 15, true);
            var plank = CreateResource("널빤지", ResourceCategory.Mineral, 1.4f, 35, false);
            var wheel = CreateResource("수레바퀴", ResourceCategory.Mineral, 1.5f, 90, false);
            CreateRecipe(log, plank, 15f, 1);
            CreateRecipe(plank, wheel, 20f, 2);

            // 4. 특산품류
            var mulberry = CreateResource("닥나무", ResourceCategory.Specialty, 1.0f, 25, true);
            var hanji = CreateResource("한지", ResourceCategory.Specialty, 0.7f, 70, false);
            var book = CreateResource("서책", ResourceCategory.Specialty, 0.9f, 180, false);
            CreateRecipe(mulberry, hanji, 12f, 1);
            CreateRecipe(hanji, book, 18f, 2);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ContentDataGenerator] 모든 기획 데이터 생성 완료!");
        }

        private void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder(RootPath)) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder($"{RootPath}/Resources")) AssetDatabase.CreateFolder(RootPath, "Resources");
            if (!AssetDatabase.IsValidFolder($"{RootPath}/Recipes")) AssetDatabase.CreateFolder(RootPath, "Recipes");
        }

        private ResourceDataSO CreateResource(string name, ResourceCategory category, float weight, int value, bool isRaw)
        {
            string path = $"{RootPath}/Resources/{name}.asset";
            ResourceDataSO asset = AssetDatabase.LoadAssetAtPath<ResourceDataSO>(path);
            if (asset == null)
            {
                asset = CreateInstance<ResourceDataSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.DisplayName = name;
            asset.Category = category;
            asset.WeightMultiplier = weight;
            asset.BaseValue = value;
            asset.IsRawResource = isRaw;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private void CreateRecipe(ResourceDataSO input, ResourceDataSO output, float time, int tier)
        {
            string name = $"{input.DisplayName}_to_{output.DisplayName}";
            string path = $"{RootPath}/Recipes/{name}.asset";
            RecipeDataSO asset = AssetDatabase.LoadAssetAtPath<RecipeDataSO>(path);
            if (asset == null)
            {
                asset = CreateInstance<RecipeDataSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.InputResource = input;
            asset.OutputResource = output;
            asset.BaseProcessingTime = time;
            asset.ProcessingTier = tier;
            EditorUtility.SetDirty(asset);
        }
    }
}
