#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 로드 시 및 Tools 메뉴에서 실행
// 1) Assets/Resources/AI_Trainer.prefab 자동 생성
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
        if (EditorApplication.isPlayingOrWillChangePlaymode) return; // 재생 중에는 씬을 고칠 수 없다
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

        // 씬의 TrainerAppear에는 연결하지 않는다. 코치 아바타는 TrainerAppear가 실행할 때 Resources에서 직접 불러온다.
        // (프리팹 에셋을 씬에 저장하면 'Prefab 참조' 경고가 난다)
    }
}
#endif
