#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Diagnostics;

// 깃허브 원클릭 자동 백업
public static class MiriGitAutoPush
{
    [MenuItem("미리와봄/깃허브에 전체 백업하기 (Commit & Push)")]
    public static void PushToGitHub()
    {
        // 1. 현재 씬 및 프로젝트 저장
        EditorApplication.ExecuteMenuItem("File/Save");
        AssetDatabase.SaveAssets();

        UnityEngine.Debug.Log("[미리와봄] 깃허브 백업을 시작합니다...");

        // 2. Git 명령어 실행 (add -> commit -> push)
        string time = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        string command = $"/c git add . && git commit -m \"자동 백업: {time}\" && git push";

        ProcessStartInfo processInfo = new ProcessStartInfo("cmd.exe", command);
        processInfo.CreateNoWindow = true;
        processInfo.UseShellExecute = false;
        processInfo.RedirectStandardOutput = true;
        processInfo.RedirectStandardError = true;
        processInfo.WorkingDirectory = Application.dataPath.Replace("/Assets", "");

        using (Process process = Process.Start(processInfo))
        {
            process.WaitForExit();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            if (process.ExitCode == 0)
            {
                UnityEngine.Debug.Log("[미리와봄] 깃허브 백업 성공!\n" + output);
                EditorUtility.DisplayDialog("미리와봄 백업", "깃허브에 성공적으로 업로드되었습니다.", "확인");
            }
            else
            {
                UnityEngine.Debug.LogError("[미리와봄] 깃허브 백업 실패:\n" + error);
                EditorUtility.DisplayDialog("미리와봄 백업 오류", "업로드에 실패했습니다. Console 창의 에러 로그를 확인하세요.", "확인");
            }
        }
    }
}
#endif