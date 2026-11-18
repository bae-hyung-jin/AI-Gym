using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 브랜치를 보고 만들고 고른 뒤, 그 브랜치로 커밋·푸시하는 에디터 창. 메뉴: Tools > Git Auto Commit
public class GitAutoCommit : EditorWindow
{
    struct GitResult
    {
        public int code; public string output, error;
        public bool Ok => code == 0;
        public string All => (output + "\n" + error).Trim();
    }

    struct BranchItem { public string name; public bool remoteOnly; } // remoteOnly: 내 컴퓨터엔 없고 origin에만 있는 브랜치

    string commitMessage = "Auto commit from Unity Editor";
    string newBranchName = "";
    string statusLog = "준비 완료. 작업할 버튼을 눌러주셔유.";
    Vector2 scrollPos;

    bool isRepo;
    string currentBranch = "";
    readonly List<BranchItem> branches = new List<BranchItem>();
    string[] branchLabels = new string[0];
    int branchIndex;

    // 사용자가 고른 저장소 폴더. 비어 있으면 프로젝트 폴더에서 위로 올라가며 .git을 찾는다.
    static string repoDir = "";
    string repoTop = ""; // git이 알려 준 저장소 최상위 폴더
    List<string> nearby = new List<string>();

    // ZIP으로 받은 폴더처럼 .git이 없을 때 GitHub 저장소와 연결하는 데 쓰는 값
    string remoteUrl = "https://github.com/bae-hyung-jin/AI-Gym.git";
    string remoteBranch = "main";
    string workBranch = ""; // 연결한 뒤 이 이름으로 새 브랜치를 만들어 거기서 작업한다(비워 두면 기준 브랜치 그대로)
    bool hasCommits = true;

    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    static string PrefKey => "GitAutoCommit.RepoDir:" + ProjectRoot;

    static string WorkingDir
    {
        get
        {
            if (!string.IsNullOrEmpty(repoDir) && Directory.Exists(repoDir)) return repoDir;
            return FindRepoUpwards(ProjectRoot) ?? ProjectRoot;
        }
    }

    static bool HasGit(string dir) => Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git"));

    static string FindRepoUpwards(string start)
    {
        for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
            if (HasGit(d.FullName)) return d.FullName;
        return null;
    }

    // 프로젝트 폴더와 같은 위치에 있는 다른 폴더들 중 .git이 있는 것(예: 원래 작업하던 폴더)을 찾는다.
    internal static List<string> FindNearbyRepos()
    {
        var found = new List<string>();
        var parent = Directory.GetParent(ProjectRoot);
        if (parent == null) return found;
        try { foreach (var d in Directory.GetDirectories(parent.FullName)) if (HasGit(d)) found.Add(d); }
        catch (System.Exception) { /* 권한이 없는 폴더는 건너뛴다 */ }
        return found;
    }

    [MenuItem("Tools/Git Auto Commit")]
    public static void ShowWindow()
    {
        GetWindow<GitAutoCommit>("Git Auto Commit");
    }

    void OnEnable()
    {
        repoDir = EditorPrefs.GetString(PrefKey, "");
        RefreshBranches();
    }

    void OnFocus() => RefreshBranches(); // 창을 다시 눌렀을 때 브랜치 목록을 새로 읽는다

    void SetRepo(string path)
    {
        repoDir = path;
        EditorPrefs.SetString(PrefKey, path);
        nearby.Clear();
        RefreshBranches();
    }

    void PickRepoFolder()
    {
        var start = string.IsNullOrEmpty(repoDir) ? Directory.GetParent(ProjectRoot).FullName : repoDir;
        var picked = EditorUtility.OpenFolderPanel("Git 저장소 폴더 선택 (.git이 들어 있는 폴더)", start, "");
        if (string.IsNullOrEmpty(picked)) return; // 취소
        var root = HasGit(picked) ? picked : FindRepoUpwards(picked); // 저장소 안쪽 폴더를 골라도 위로 올라가 찾아 준다
        if (root == null) { statusLog = $"[안내] '{picked}' 에는 .git이 없어요. 저장소 폴더(.git이 보이는 곳)를 골라 주세유."; return; }
        SetRepo(root);
        statusLog = $"[성공] 저장소 폴더를 '{root}'(으)로 정했슈.";
    }

    void OnGUI()
    {
        GUILayout.Label("Unity Git 자동 커밋 & 푸시 툴 (춘식2 튜닝판)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        DrawRepoSection();
        EditorGUILayout.Space();

        if (!isRepo)
        {
            EditorGUILayout.HelpBox("이 폴더는 Git 저장소가 아니거나 git을 찾을 수 없어요.\n위의 '폴더 선택…'이나 '근처에서 찾기'로 .git이 있는 저장소 폴더를 정하거나, 아래에서 이 폴더를 GitHub 저장소에 연결해 주세요.", MessageType.Warning);
            if (GUILayout.Button("다시 확인", GUILayout.Height(28))) RefreshBranches();
            EditorGUILayout.Space();
            DrawConnectSection();
            DrawLog();
            return;
        }

        if (!hasCommits)
        {
            EditorGUILayout.HelpBox("저장소는 만들어졌지만 아직 GitHub와 연결이 끝나지 않았어요(커밋이 없어요). 아래에서 연결을 마무리해 주세요. 마무리 전에는 커밋·푸시하지 마세요.", MessageType.Warning);
            DrawConnectSection();
            DrawLog();
            return;
        }

        DrawBranchSection();
        EditorGUILayout.Space();
        DrawCommitSection();
        DrawLog();
    }

    // ---- 저장소 폴더 ----
    void DrawRepoSection()
    {
        EditorGUILayout.LabelField("저장소 폴더", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(WorkingDir, EditorStyles.textField, GUILayout.Height(18));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("폴더 선택…")) { PickRepoFolder(); GUIUtility.ExitGUI(); }
        if (GUILayout.Button("근처에서 찾기"))
        {
            nearby = FindNearbyRepos();
            statusLog = nearby.Count == 0
                ? "[안내] 근처에서 .git이 있는 폴더를 못 찾았슈. '폴더 선택…'으로 직접 골라 주세유."
                : $"[안내] 근처에서 저장소 {nearby.Count}개를 찾았슈. 아래에서 골라 주세유.";
        }
        if (GUILayout.Button("자동으로 되돌리기")) { SetRepo(""); GUIUtility.ExitGUI(); }
        EditorGUILayout.EndHorizontal();

        string choose = null;
        foreach (var r in nearby) if (GUILayout.Button("→ " + r)) choose = r;
        if (choose != null) { SetRepo(choose); statusLog = $"[성공] 저장소 폴더를 '{choose}'(으)로 정했슈."; GUIUtility.ExitGUI(); }

        if (isRepo && !ProjectInsideRepo())
            EditorGUILayout.HelpBox($"지금 열려 있는 Unity 프로젝트({ProjectRoot})가 이 저장소 안에 없어요.\n여기서 커밋·푸시해도 이 프로젝트에서 고친 내용은 올라가지 않아요. 이 프로젝트 파일이 들어 있는 저장소를 골라 주세요.", MessageType.Warning);
    }

    // ---- 이 폴더를 GitHub 저장소에 연결 ----
    void DrawConnectSection()
    {
        EditorGUILayout.LabelField("이 폴더를 GitHub 저장소에 연결", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("ZIP으로 받은 폴더처럼 .git이 없을 때 쓰세요. 파일은 지우거나 바꾸지 않고, GitHub의 기준 브랜치와 비교해 달라진 부분을 변경 사항으로 보여 줘요.", MessageType.Info);
        remoteUrl = EditorGUILayout.TextField("GitHub 주소", remoteUrl);
        remoteBranch = EditorGUILayout.TextField("기준 브랜치(GitHub에 있는 것)", remoteBranch);
        workBranch = EditorGUILayout.TextField("작업할 새 브랜치(선택)", workBranch);
        EditorGUILayout.HelpBox("기준 브랜치는 GitHub에 이미 있는 이름(보통 main)이어야 해요. 새로 만들 브랜치 이름은 아래 '작업할 새 브랜치'에 쓰면 연결 뒤에 자동으로 만들어서 옮겨 줘요.", MessageType.None);
        if (GUILayout.Button("이 폴더를 저장소에 연결", GUILayout.Height(30)))
        {
            ConnectToRemote();
            GUIUtility.ExitGUI();
        }
    }

    void ConnectToRemote()
    {
        var url = remoteUrl.Trim();
        var branch = remoteBranch.Trim();
        var root = ProjectRoot;
        if (!(url.StartsWith("https://") || url.StartsWith("http://") || url.StartsWith("git@") || url.StartsWith("ssh://")))
        { statusLog = "[안내] GitHub 주소를 https://… 또는 git@… 형태로 써 주세유."; return; }
        if (branch.Length == 0 || !GitIn(root, "check-ref-format", "--branch", branch).Ok)
        { statusLog = "[안내] 기준 브랜치 이름이 올바르지 않아요. 보통은 main이에요."; return; }
        var work = workBranch.Trim();
        if (work.Length > 0 && !GitIn(root, "check-ref-format", "--branch", work).Ok)
        { statusLog = "[안내] 작업할 새 브랜치 이름이 올바르지 않아요. 공백·특수문자를 피해 주세요."; return; }

        if (!EditorUtility.DisplayDialog("이 폴더를 저장소에 연결할까요?",
            $"폴더: {root}\n\n여기에 .git을 만들고 아래 저장소의 '{branch}' 브랜치와 연결해요.\n{url}\n\n파일은 지우거나 바꾸지 않아요. 연결 뒤에는 GitHub와 달라진 파일이 변경 사항으로 보여요.\n(저장소 크기에 따라 가져오는 데 몇 분 걸릴 수 있어요.)",
            "연결", "취소"))
            return;

        var log = new StringBuilder();
        try
        {
            EditorUtility.DisplayProgressBar("Git 연결", "저장소 만드는 중 (git init)", 0.1f);
            if (!HasGit(root))
            {
                var init = GitIn(root, "init");
                if (!init.Ok) { Fail("git init", init); return; }
                log.AppendLine("- 저장소를 만들었슈 (git init).");
            }

            var hasCommit = GitIn(root, "rev-parse", "--verify", "HEAD").Ok;
            if (!hasCommit) GitIn(root, "symbolic-ref", "HEAD", "refs/heads/" + branch); // 아직 커밋이 없으면 현재 브랜치 이름을 기준 브랜치와 맞춘다

            EditorUtility.DisplayProgressBar("Git 연결", "원격 주소 등록", 0.3f);
            var hasRemote = GitIn(root, "remote", "get-url", "origin").Ok;
            var rem = hasRemote ? GitIn(root, "remote", "set-url", "origin", url) : GitIn(root, "remote", "add", "origin", url);
            if (!rem.Ok) { Fail("원격 주소 등록", rem); return; }
            log.AppendLine("- 원격(origin)을 연결했슈: " + url);

            EditorUtility.DisplayProgressBar("Git 연결", $"GitHub에서 '{branch}' 가져오는 중 (git fetch)… 시간이 걸려유", 0.5f);
            var fetch = GitIn(root, "fetch", "origin", branch);
            if (!fetch.Ok)
            {
                var heads = GitIn(root, "ls-remote", "--heads", "origin").output; // 어떤 브랜치가 있는지 알려 준다
                var names = new List<string>();
                foreach (var line in heads.Split('\n')) { var i = line.IndexOf("refs/heads/"); if (i >= 0) names.Add(line.Substring(i + "refs/heads/".Length).Trim()); }
                var hint = names.Count > 0 ? "\nGitHub에 있는 브랜치: " + string.Join(", ", names) : "";
                Fail("git fetch (주소·권한·브랜치 이름을 확인해 주세요)", new GitResult { code = fetch.code, output = fetch.output, error = fetch.error + hint });
                return;
            }
            if (!GitIn(root, "rev-parse", "--verify", "origin/" + branch).Ok)
            { Fail("기준 브랜치 확인", new GitResult { code = 1, error = $"origin/{branch} 를 찾지 못했어요. 브랜치 이름을 확인해 주세요." }); return; }
            log.AppendLine($"- GitHub의 '{branch}' 브랜치를 가져왔슈.");

            if (!hasCommit)
            {
                EditorUtility.DisplayProgressBar("Git 연결", "현재 파일과 GitHub 기준 맞추는 중", 0.85f);
                var reset = GitIn(root, "reset", "origin/" + branch); // 파일은 그대로 두고 기록만 GitHub와 맞춘다(mixed)
                if (!reset.Ok) { Fail("git reset", reset); return; }
                GitIn(root, "branch", "--set-upstream-to=origin/" + branch, branch);
                log.AppendLine("- 파일은 그대로 두고 기록만 GitHub와 맞췄슈.");
            }

            if (work.Length > 0 && work != branch)
            {
                var sw = GitIn(root, "switch", "-c", work); // 기준 브랜치에서 갈라져 나온 새 브랜치로 옮긴다(파일은 그대로)
                if (!sw.Ok) { Fail($"새 브랜치 '{work}' 만들기", sw); return; }
                log.AppendLine($"- 새 브랜치 '{work}'를 만들고 옮겼슈.");
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        SetRepo(""); // 이제 이 폴더가 저장소이니 자동 탐색으로 돌린다
        var changed = Git("status", "--porcelain").output.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
        statusLog = $"[성공] 연결 완료!\n{log}\nGitHub 기준과 달라진 파일: {changed}개\n올리기 전에 'git status'나 변경 목록으로 어떤 파일이 달라졌는지 꼭 확인해 주세유.";
        UnityEngine.Debug.Log("[Git Connect] " + statusLog);
    }

    void Fail(string step, GitResult r)
    {
        statusLog = $"[오류 발생] {step}\n{r.All}";
        UnityEngine.Debug.LogError($"[Git Connect Error] {step}\n{r.All}");
        RefreshBranches();
    }

    bool ProjectInsideRepo()
    {
        if (string.IsNullOrEmpty(repoTop)) return true;
        var top = Path.GetFullPath(repoTop).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var proj = Path.GetFullPath(ProjectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return proj.StartsWith(top, System.StringComparison.OrdinalIgnoreCase);
    }

    // ---- 브랜치 ----
    void DrawBranchSection()
    {
        EditorGUILayout.LabelField("브랜치", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("현재 브랜치", string.IsNullOrEmpty(currentBranch) ? "(분리된 HEAD — 브랜치가 아닙니다)" : currentBranch, EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        branchIndex = EditorGUILayout.Popup("이동할 브랜치", Mathf.Clamp(branchIndex, 0, Mathf.Max(0, branchLabels.Length - 1)), branchLabels);
        if (GUILayout.Button("이동", GUILayout.Width(60))) SwitchToSelected();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("목록 새로고침")) RefreshBranches();
        if (GUILayout.Button("원격 가져오기 (fetch)"))
        {
            Report(Git("fetch", "origin", "--prune"), "원격 브랜치 목록을 가져왔슈.");
            RefreshBranches();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        newBranchName = EditorGUILayout.TextField("새 브랜치 이름", newBranchName);
        EditorGUILayout.HelpBox($"새 브랜치는 지금 있는 '{(string.IsNullOrEmpty(currentBranch) ? "현재 위치" : currentBranch)}'에서 갈라져 나와요. 이름에 공백은 쓸 수 없어요. 예) feature/intro-screen", MessageType.None);
        if (GUILayout.Button("새 브랜치 만들고 이동", GUILayout.Height(26))) CreateBranch();
    }

    void RefreshBranches()
    {
        var inside = Git("rev-parse", "--is-inside-work-tree");
        isRepo = inside.Ok && inside.output.Trim() == "true";
        branches.Clear();
        if (!isRepo) { branchLabels = new string[0]; currentBranch = ""; repoTop = ""; Repaint(); return; }

        repoTop = Git("rev-parse", "--show-toplevel").output.Trim().Replace('/', Path.DirectorySeparatorChar);
        hasCommits = Git("rev-parse", "--verify", "HEAD").Ok;
        currentBranch = Git("branch", "--show-current").output.Trim();

        var local = new HashSet<string>();
        foreach (var line in Git("branch", "--format=%(refname:short)").output.Split('\n'))
        {
            var n = line.Trim();
            if (n.Length == 0) continue;
            local.Add(n);
            branches.Add(new BranchItem { name = n });
        }
        foreach (var line in Git("branch", "-r", "--format=%(refname:short)").output.Split('\n'))
        {
            var n = line.Trim();
            if (!n.StartsWith("origin/") || n == "origin/HEAD" || n.EndsWith("/HEAD")) continue;
            n = n.Substring("origin/".Length);
            if (n.Length == 0 || local.Contains(n)) continue;
            branches.Add(new BranchItem { name = n, remoteOnly = true });
        }

        branchLabels = new string[branches.Count];
        branchIndex = 0;
        for (var i = 0; i < branches.Count; i++)
        {
            branchLabels[i] = branches[i].name + (branches[i].remoteOnly ? "   (원격에만 있음)" : "");
            if (branches[i].name == currentBranch) { branchIndex = i; branchLabels[i] += "   ← 현재"; }
        }
        Repaint();
    }

    void SwitchToSelected()
    {
        if (branches.Count == 0) { statusLog = "[안내] 고를 수 있는 브랜치가 없슈."; return; }
        var b = branches[Mathf.Clamp(branchIndex, 0, branches.Count - 1)];
        if (b.name == currentBranch) { statusLog = $"[안내] 이미 '{b.name}' 브랜치에 있슈."; return; }
        if (!AskSaveScenes()) return;

        if (HasChanges() && !EditorUtility.DisplayDialog("커밋 안 한 변경이 있어요",
            "그대로 브랜치를 옮기면 변경 내용이 새 브랜치로 따라가거나, 겹치는 파일이 있으면 이동이 막힐 수 있어요.\n먼저 커밋하고 옮기는 걸 추천해요.", "그래도 이동", "취소"))
            return;

        var r = b.remoteOnly ? Git("switch", "--track", "origin/" + b.name) : Git("switch", b.name);
        Report(r, $"'{b.name}' 브랜치로 옮겼슈.");
        RefreshBranches();
        AssetDatabase.Refresh(); // 파일이 바뀌었으니 Unity가 다시 읽게 한다
    }

    void CreateBranch()
    {
        var name = newBranchName.Trim();
        if (name.Length == 0) { statusLog = "[안내] 새 브랜치 이름을 먼저 써 주세유."; return; }
        if (!Git("check-ref-format", "--branch", name).Ok) { statusLog = $"[안내] '{name}'은(는) 브랜치 이름으로 쓸 수 없어요. 공백·특수문자·점(.) 연속 등을 피해 주세요."; return; }
        foreach (var b in branches) if (b.name == name) { statusLog = $"[안내] '{name}' 브랜치가 이미 있슈. 목록에서 골라 이동해 주세유."; return; }
        if (!AskSaveScenes()) return;

        var r = Git("switch", "-c", name); // 변경 내용은 그대로 새 브랜치로 따라온다
        Report(r, $"'{name}' 브랜치를 만들고 옮겼슈.");
        if (r.Ok) newBranchName = "";
        RefreshBranches();
    }

    // ---- 커밋·푸시 ----
    void DrawCommitSection()
    {
        EditorGUILayout.LabelField("커밋 & 푸시", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("올라갈 브랜치", string.IsNullOrEmpty(currentBranch) ? "(없음)" : currentBranch);
        commitMessage = EditorGUILayout.TextField("커밋 메시지", commitMessage);
        EditorGUILayout.Space();

        if (GUILayout.Button("1. 변경사항 추가 (git add .)", GUILayout.Height(30)))
        {
            AskSaveScenes();
            Report(Git("add", "."), "모든 변경사항이 스테이지에 담겼슈.");
        }
        if (GUILayout.Button("2. 커밋 실행 (git commit)", GUILayout.Height(30)))
        {
            AskSaveScenes();
            CommitChanges();
        }
        if (GUILayout.Button("3. 원격 저장소에 푸시 (git push)", GUILayout.Height(30)))
        {
            if (ConfirmProtected()) Push();
        }

        EditorGUILayout.Space();
        GUI.backgroundColor = new Color(0.3f, 1f, 0.4f);
        if (GUILayout.Button("🚀 한 번에 다 해치우기 (Add + Commit + Push)", GUILayout.Height(40))) ExecuteAll();
        GUI.backgroundColor = Color.white;
    }

    void CommitChanges()
    {
        if (!HasChanges()) { statusLog = "[안내] 바뀐 파일이 하나도 없어서 커밋할 게 없슈! 파일 수정하고 다시 눌러봐유."; return; }
        Report(Git("commit", "-m", commitMessage), "커밋 도장 쾅 찍었슈.");
    }

    void Push()
    {
        // 새 브랜치도 처음 올릴 때 원격 브랜치가 자동으로 만들어지도록 -u를 붙인다
        Report(Git("push", "-u", "origin", "HEAD"), $"'{currentBranch}' 브랜치를 원격 저장소(GitHub)로 쐈슈!");
    }

    void ExecuteAll()
    {
        if (!ConfirmProtected()) return;
        if (!AskSaveScenes()) return;

        statusLog = "원클릭 작업 시작하는 중...\n";
        var add = Git("add", ".");
        if (!add.Ok) { statusLog += "❌ add 실패:\n" + add.All; return; }

        if (HasChanges())
        {
            var c = Git("commit", "-m", commitMessage);
            if (!c.Ok) { statusLog += "❌ 커밋 실패:\n" + c.All; return; }
            statusLog += "- 변경사항 커밋 완료!\n";
        }
        else statusLog += "- 새로 바뀐 게 없어서 기존 커밋 상태로 푸시 진행해유.\n";

        var p = Git("push", "-u", "origin", "HEAD");
        if (p.Ok)
        {
            statusLog += $"🎉 '{currentBranch}' 브랜치 업로드 성공! 깃허브 가보셔유, 잘 들어갔을겨!";
            UnityEngine.Debug.Log($"[Git Success]\n{p.All}");
        }
        else
        {
            statusLog += $"❌ 푸시 중 문제 발생:\n{p.All}";
            UnityEngine.Debug.LogError($"[Git Push Error] {p.All}");
        }
        RefreshBranches();
    }

    // main/master에 바로 올리려 할 때 한 번 더 물어본다
    bool ConfirmProtected()
    {
        if (currentBranch != "main" && currentBranch != "master") return true;
        return EditorUtility.DisplayDialog("main 브랜치에 바로 올려요",
            $"지금 '{currentBranch}' 브랜치예요. 여기에 올리면 팀 모두의 기준 코드가 바로 바뀌어요.\n별도 브랜치에서 작업하고 싶으면 취소하고 새 브랜치를 만드세요.", "그래도 올리기", "취소");
    }

    // ---- 공통 ----
    void DrawLog()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("작업 상태 로그:", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(140));
        EditorGUILayout.TextArea(statusLog, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    // 저장 안 한 씬은 파일에 없어서 커밋에 안 들어간다. 먼저 저장할지 묻는다.
    bool AskSaveScenes() => EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

    bool HasChanges() => !string.IsNullOrWhiteSpace(Git("status", "--porcelain").output);

    void Report(GitResult r, string successMessage)
    {
        if (r.Ok)
        {
            statusLog = $"[성공] {successMessage}\n{r.All}";
            UnityEngine.Debug.Log($"[Git Success] {successMessage}\n{r.All}");
        }
        else
        {
            statusLog = $"[오류 발생]\n{r.All}";
            UnityEngine.Debug.LogError($"[Git Error] {r.All}");
        }
        RefreshBranches();
    }

    // 깃 실행 공통 모듈: 인자를 하나씩 넘겨 따옴표·공백이 있어도 깨지지 않고, 성공 여부는 종료 코드로 본다.
    static GitResult Git(params string[] args) => GitIn(WorkingDir, args);

    // dir 폴더에서 git을 실행한다(연결 기능은 선택한 저장소가 아니라 프로젝트 폴더에서 실행해야 해서 따로 둔다)
    static GitResult GitIn(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0"; // 로그인 입력을 기다리며 에디터가 멈추는 것을 막는다

        try
        {
            using (var p = Process.Start(psi))
            {
                var errTask = p.StandardError.ReadToEndAsync();
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return new GitResult { code = p.ExitCode, output = output, error = errTask.Result };
            }
        }
        catch (System.Exception e)
        {
            return new GitResult { code = -1, error = "git을 실행하지 못했어요: " + e.Message };
        }
    }
}
