using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;

public class GitAutoCommit : EditorWindow
{
    private string commitMessage = "Auto commit from Unity Editor";

    [MenuItem("Tools/Git Auto Commit")]
    public static void ShowWindow()
    {
        GetWindow<GitAutoCommit>("Git Auto Commit");
    }

    private void OnGUI()
    {
        GUILayout.Label("Unity Git 자동 커밋 & 푸시 툴", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        commitMessage = EditorGUILayout.TextField("커밋 메시지", commitMessage);
        
        EditorGUILayout.Space();

        if (GUILayout.Button("1. 변경사항 추가 (git add .)"))
        {
            RunGitCommand("add .", "모든 변경사항이 스테이지에 추가되었습니다.");
        }

        if (GUILayout.Button("2. 커밋 실행 (git commit)"))
        {
            RunGitCommand($"commit -m \"{commitMessage}\"", "커밋이 완료되었습니다.");
        }

        if (GUILayout.Button("3. 원격 저장소에 푸시 (git push)"))
        {
            RunGitCommand("push", "원격 저장소 푸시가 완료되었습니다.");
        }

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("한 번에 실행 (Add + Commit + Push)"))
        {
            ExecuteAll();
        }
    }

    private void RunGitCommand(string arguments, string successMessage)
    {
        // 프로젝트 루트 경로 (Assets 폴더의 상위 폴더)
        string workingDir = Directory.GetParent(Application.dataPath).FullName;

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                UnityEngine.Debug.Log($"[Git Success] {successMessage}\n{output}");
            }
            else
            {
                UnityEngine.Debug.LogError($"[Git Error] {error}");
            }
        }
    }

    private void ExecuteAll()
    {
        string workingDir = Directory.GetParent(Application.dataPath).FullName;
        
        // 1. git add .
        RunGitCommand_Silent("add .", workingDir);
        
        // 2. git commit
        RunGitCommand_Silent($"commit -m \"{commitMessage}\"", workingDir);
        
        // 3. git push
        RunGitCommand_Silent("push", workingDir);

        UnityEngine.Debug.Log("[Git] 모든 자동화 작업(Add, Commit, Push)이 요청되었습니다. 콘솔 로그를 확인하세요.");
    }

    private void RunGitCommand_Silent(string arguments, string workingDir)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                if (!string.IsNullOrEmpty(output)) UnityEngine.Debug.Log($"[Git {arguments}] {output}");
            }
            else
            {
                if (!string.IsNullOrEmpty(error)) UnityEngine.Debug.LogError($"[Git {arguments} Error] {error}");
            }
        }
    }
}