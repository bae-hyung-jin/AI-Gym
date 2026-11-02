using UnityEngine;

// 애니메이션 이벤트는 Animator와 같은 오브젝트로만 전달되므로 EagleHuman에 붙인다.
// 받는 함수가 없으면 걷고 뛸 때마다 "has no receiver" 오류가 뜬다.
public class AnimationEventReceiver : MonoBehaviour
{
    public AudioClip[] footstepClips;
    public AudioClip landingClip;
    [Range(0f, 1f)] public float volume = 0.5f;

    const string SfxFolder = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Sfx";

    void OnFootstep(AnimationEvent animationEvent)
    {
        // 걷기와 달리기가 블렌드되면 두 클립이 모두 이벤트를 보내므로 비중이 큰 쪽만 소리를 낸다.
        if (animationEvent.animatorClipInfo.weight < 0.5f) return;
        if (footstepClips == null || footstepClips.Length == 0) return;
        var clip = footstepClips[Random.Range(0, footstepClips.Length)];
        if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }

    void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight < 0.5f) return;
        if (landingClip != null) AudioSource.PlayClipAtPoint(landingClip, transform.position, volume);
    }

#if UNITY_EDITOR
    // 클립이 비어 있으면 Starter Assets의 발소리를 자동으로 채운다. 빌드에 넣으려면 씬을 한 번 저장한다.
    void OnValidate()
    {
        if (footstepClips == null || footstepClips.Length == 0)
        {
            var list = new System.Collections.Generic.List<AudioClip>();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("Player_Footstep t:AudioClip", new[] { SfxFolder }))
                list.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)));
            footstepClips = list.ToArray();
        }
        if (landingClip == null)
            landingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + "/Player_Land.wav");
    }
#endif
}
