using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Text;

public class GitAutoCommit : EditorWindow
{
    private string commitMessage = "Auto commit from Unity Editor";
    private string statusLog = "준비 완료. 작업할 버튼을 눌러주셔유.";
    private Vector2 scrollPos;

    [MenuItem("Tools/Git Auto Commit")]
    public static void ShowWindow()
    {
        GetWindow<GitAutoCommit>("Git Auto Commit");
    }

    private void OnGUI()
    {
        GUILayout.Label("Unity Git 자동 커밋 & 푸시 툴 (춘식2 튜닝판)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        commitMessage = EditorGUILayout.TextField("커밋 메시지", commitMessage);
        EditorGUILayout.Space();

        if (GUILayout.Button("1. 변경사항 추가 (git add .)", GUILayout.Height(30)))
        {
            RunGitCommand("add .", "모든 변경사항이 스테이지에 담겼슈.");
        }

        if (GUILayout.Button("2. 커밋 실행 (git commit)", GUILayout.Height(30)))
        {
            CommitChanges();
        }

        if (GUILayout.Button("3. 원격 저장소에 푸시 (git push)", GUILayout.Height(30)))
        {
            // 현재 작업 중인 브랜치로 안전하게 쏘아 올리기
            RunGitCommand("push origin HEAD", "원격 저장소(GitHub)로 안전하게 쐈슈!");
        }

        EditorGUILayout.Space();
        GUI.backgroundColor = new Color(0.3f, 1f, 0.4f);
        if (GUILayout.Button("🚀 한 번에 다 해치우기 (Add + Commit + Push)", GUILayout.Height(40)))
        {
            ExecuteAll();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("작업 상태 로그:", EditorStyles.boldLabel);
        
        // 결과 로그 박스
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(120));
        EditorGUILayout.TextArea(statusLog, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // 커밋 실행 전 변경점이 있는지 먼저 검사하는 똑똑한 함수
    private void CommitChanges()
    {
        string status = RunGitProcess("status --porcelain");
        if (string.IsNullOrWhiteSpace(status))
        {
            statusLog = "[안내] 바뀐 파일이 하나도 없어서 커밋할 게 없슈! 파일 수정하고 다시 눌러봐유.";
            UnityEngine.Debug.LogWarning(statusLog);
            return;
        }

        RunGitCommand($"commit -m \"{commitMessage}\"", "커밋 도장 쾅 찍었슈.");
    }

    private void ExecuteAll()
    {
        string workingDir = Directory.GetParent(Application.dataPath).FullName;

        statusLog = "원클릭 작업 시작하는 중...\n";
        
        // 1. Add
        RunGitProcess("add .");
        
        // 2. 바뀐 점 확인 후 Commit
        string status = RunGitProcess("status --porcelain");
        if (!string.IsNullOrWhiteSpace(status))
        {
            RunGitProcess($"commit -m \"{commitMessage}\"");
            statusLog += "- 변경사항 커밋 완료!\n";
        }
        else
        {
            statusLog += "- 새로 바뀐 게 없어서 기존 커밋 상태로 푸시 진행해유.\n";
        }

        // 3. Push
        string pushError;
        string pushOutput = RunGitProcess("push origin HEAD", out pushError);

        if (string.IsNullOrEmpty(pushError) || pushError.Contains("Everything up-to-date") || pushError.Contains("->"))
        {
            statusLog += "🎉 GitHub 업로드 성공! 깃허브 가보셔유, 잘 들어갔을겨!";
            UnityEngine.Debug.Log($"[Git Success]\n{pushOutput}\n{pushError}");
        }
        else
        {
            statusLog += $"❌ 푸시 중 문제 발생:\n{pushError}";
            UnityEngine.Debug.LogError($"[Git Push Error] {pushError}");
        }
    }

    private void RunGitCommand(string arguments, string successMessage)
    {
        string error;
        string output = RunGitProcess(arguments, out error);

        if (string.IsNullOrEmpty(error) || error.Contains("Everything up-to-date") || error.Contains("->"))
        {
            statusLog = $"[성공] {successMessage}\n{output}";
            UnityEngine.Debug.Log($"[Git Success] {successMessage}\n{output}");
        }
        else
        {
            statusLog = $"[오류 발생]\n{error}";
            UnityEngine.Debug.LogError($"[Git Error] {error}");
        }
    }

    // 깃 프로세스 실행 공통 모듈 (UTF-8 한글 지원 & 에러 분리)
    private string RunGitProcess(string arguments)
    {
        string error;
        return RunGitProcess(arguments, out error);
    }

    private string RunGitProcess(string arguments, out string errorOutput)
    {
        string workingDir = Directory.GetParent(Application.dataPath).FullName;

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            errorOutput = error;
            return output;
        }
    }
}