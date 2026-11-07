#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 로드 시 및 Tools 메뉴에서 실행
// 1) Assets/Resources/AI_Trainer.prefab 자동 생성
// 2) 씬의 TrainerAppear 컴포넌트에 Coach Prefab 자동 할당
[InitializeOnLoad]
public static class CoachSetup
{
    const string ModelPath = "Assets/Gym/AI_Trainer.fbx";
    const string ResourcesDir = "Assets/Resources";
    const string PrefabPath = "Assets/Resources/AI_Trainer.prefab";

    static CoachSetup()
    {
        EditorApplication.delayCall += EnsureSetup;
    }

    [MenuItem("Tools/AI Trainer/Coach 아바타 연결 및 프리팹 생성")]
    public static void EnsureSetup()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) return;

        if (!AssetDatabase.IsValidFolder(ResourcesDir))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            prefab = PrefabUtility.SaveAsPrefabAsset(model, PrefabPath);
            Debug.Log($"[CoachSetup] {PrefabPath} 프리팹 생성 완료.");
        }

        var targets = Object.FindObjectsByType<TrainerAppear>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in targets)
        {
            var so = new SerializedObject(t);
            var prop = so.FindProperty("coachPrefab");
            if (prop != null)
            {
                var cur = prop.objectReferenceValue;
                if (cur == null || cur.name != model.name)
                {
                    prop.objectReferenceValue = prefab != null ? (Object)prefab : (Object)model;
                    so.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                    Debug.Log($"[CoachSetup] {t.gameObject.name}의 TrainerAppear에 Coach Prefab 자동 연결 완료.");
                }
            }
        }
    }
}
#endif
