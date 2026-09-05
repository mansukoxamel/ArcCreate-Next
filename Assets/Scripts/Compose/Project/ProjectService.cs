using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Collections.Concurrent;
#endif
using ArcCreate.ChartFormat;
using ArcCreate.Compose.Navigation;
using ArcCreate.Compose.Popups;
using ArcCreate.Data;
using ArcCreate.Gameplay;
using ArcCreate.Utility;
using ArcCreate.Utility.Extension;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArcCreate.Compose.Project
{
    [EditorScope("Project")]
    public class ProjectService : MonoBehaviour, IProjectService
    {
        private const string RecentDirectoriesPlayerPrefKey = "Startup.RecentProjects";
        private const int MaxRecentDirectoryCount = 10;
        [SerializeField] private GameplayData gameplayData;
        [SerializeField] private Button newProjectButton;
        [SerializeField] private Button openProjectButton;
        [SerializeField] private Button saveProjectButton;
        [SerializeField] private Button openFolderButton;
        [SerializeField] private CanvasGroup toggleInteractiveCanvas;
        [SerializeField] private GameObject noProjectLoadedHint;
        [SerializeField] private ChartPicker chartPicker;
        [SerializeField] private TMP_Text currentChartPath;
        [SerializeField] private NewProjectDialog newProjectDialog;
        [SerializeField] private NewChartDialog newChartDialog;
        [SerializeField] private RawEditor rawEditor;
        [SerializeField] private List<Color> defaultDifficultyColors;
        [SerializeField] private List<string> defaultDifficultyNames;
        private AutosaveHelper autosaveHelper;
        private Button recentDirectoriesButton;
        private bool isDirectFileProject;
        private Action<AudioClip> pendingAudioSwitch;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private readonly ConcurrentQueue<IReadOnlyList<string>> droppedFileBatches = new ConcurrentQueue<IReadOnlyList<string>>();
        private WindowsFileDrop windowsFileDrop;
#endif

        public event Action<ChartSettings> OnChartLoad;

        public event Action<ProjectSettings> OnProjectLoad;

        public ProjectSettings CurrentProject { get; private set; }

        public ChartSettings CurrentChart { get; private set; }

        public List<Color> DefaultDifficultyColors => defaultDifficultyColors;

        public void CreateNewProject(NewProjectInfo info)
        {
            isDirectFileProject = false;
            string projPath = info.ProjectFile.FullPath;
            if (!projPath.EndsWith(".arcproj"))
            {
                projPath = projPath + ".arcproj";
            }

            string dir = Path.GetDirectoryName(projPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (info.AudioPath.ShouldCopy)
            {
                if (!File.Exists(info.AudioPath.OriginalPath))
                {
                    throw new ComposeException(I18n.S("Compose.Exception.FileDoesNotExist", new Dictionary<string, object>
                    {
                        { "Path", info.AudioPath.OriginalPath },
                    }));
                }

                info.AudioPath.RenameUntilNoOverwrite();
                File.Copy(info.AudioPath.OriginalPath, info.AudioPath.FullPath);
            }
            else
            {
                if (!File.Exists(info.AudioPath.FullPath))
                {
                    throw new ComposeException(I18n.S("Compose.Exception.FileDoesNotExist", new Dictionary<string, object>
                    {
                        { "Path", info.AudioPath.FullPath },
                    }));
                }
            }

            if (info.BackgroundPath?.ShouldCopy ?? false)
            {
                info.BackgroundPath.RenameUntilNoOverwrite();
                File.Copy(info.BackgroundPath.OriginalPath, info.BackgroundPath.FullPath);
            }

            if (info.JacketPath?.ShouldCopy ?? false)
            {
                info.JacketPath.RenameUntilNoOverwrite();
                File.Copy(info.JacketPath.OriginalPath, info.JacketPath.FullPath);
            }

            ChartSettings chart = new ChartSettings()
            {
                ChartPath = info.StartingChartPath,
                BaseBpm = info.BaseBPM,
                AudioPath = info.AudioPath.ShortenedPath,
                JacketPath = info.JacketPath?.ShortenedPath,
                BackgroundPath = info.BackgroundPath?.ShortenedPath,
            };

            ProjectSettings projectSettings = new ProjectSettings()
            {
                Path = projPath,
                LastOpenedChartPath = info.StartingChartPath,
                Charts = new List<ChartSettings>() { chart },
            };

            AutofillChart(chart);
            SerializeProject(projectSettings);
            OpenProject(projectSettings.Path);

            Debug.Log(
                I18n.S("Compose.Notify.Project.NewProject", new Dictionary<string, object>()
                {
                    { "Path", projectSettings.Path },
                }));
        }

        public void CreateNewChart(string chartFilePath)
        {
            ChartSettings newChart = CurrentChart.Clone();
            newChart.ChartPath = chartFilePath;
            AutofillChart(newChart);

            CurrentProject.Charts.Add(newChart);
            CurrentProject.Charts.Sort((a, b) => a.ChartPath.CompareTo(b.ChartPath));

            CurrentChart = newChart;
            CurrentProject.LastOpenedChartPath = newChart.ChartPath;

            chartPicker.SetOptions(CurrentProject.Charts, CurrentChart);
            currentChartPath.text = CurrentChart.ChartPath;

            LoadChart(CurrentChart);
            Debug.Log(
                I18n.S("Compose.Notify.Project.CreateChart", new Dictionary<string, object>()
                {
                    { "Path", chartFilePath },
                }));
        }

        public void OpenChart(ChartSettings chart)
        {
            if (chart == null || chart == CurrentChart)
            {
                return;
            }

            OpenUnsavedChangesDialog(() => SwitchChart(chart));
        }

        private void SwitchChart(ChartSettings chart)
        {
            int currentTiming = Services.Gameplay.Audio.AudioTiming;
            string currentAudioPath = GetAbsoluteAudioPath(CurrentChart);
            string nextAudioPath = GetAbsoluteAudioPath(chart);
            bool usesSameAudio = DirectFileProjectResolver.AreSameFile(currentAudioPath, nextAudioPath);

            CurrentChart.LastWorkingTiming = currentTiming;
            CurrentChart = chart;
            CurrentProject.LastOpenedChartPath = CurrentChart.ChartPath;
            currentChartPath.text = CurrentChart.ChartPath;

            if (!usesSameAudio)
            {
                Services.Gameplay.Audio.Pause();
            }

            LoadChart(CurrentChart, usesSameAudio, !usesSameAudio);
        }

        public void RemoveChart(ChartSettings chart)
        {
            if (chart == CurrentChart)
            {
                return;
            }

            CurrentProject.Charts.Remove(chart);
            chartPicker.SetOptions(CurrentProject.Charts, CurrentChart);

            Debug.Log(
                I18n.S("Compose.Notify.Project.RemoveChart", new Dictionary<string, object>()
                {
                    { "Path", chart.ChartPath },
                }));
        }

        [EditorAction("New", false, "<c-n>")]
        [KeybindHint(Exclude = true)]
        public void StartCreatingNewProject()
        {
            OpenUnsavedChangesDialog(newProjectDialog.Open);
        }

        [EditorAction("Open", false, "<c-o>")]
        [KeybindHint(Exclude = true)]
        public void StartOpeningProject()
        {
            OpenUnsavedChangesDialog(OnOpenConfirmed);
        }

        [EditorAction("Save", false, "<c-s>")]
        [KeybindHint(Exclude = true)]
        [RequireGameplayLoaded]
        public void SaveProject()
        {
            if (CurrentProject == null)
            {
                return;
            }

            CurrentChart.LastWorkingTiming = Services.Gameplay.Audio.AudioTiming;
            SerializeChart(CurrentProject);
            if (!isDirectFileProject)
            {
                SerializeProject(CurrentProject);
            }

            Values.ProjectModified = false;
        }

        [EditorAction("Reload", false, "<c-s-r>")]
        [KeybindHint(Exclude = true)]
        [RequireGameplayLoaded]
        public void ReloadChart()
        {
            if (CurrentProject == null)
            {
                return;
            }

            int currentTiming = Services.Gameplay.Audio.AudioTiming;
            SaveProject();
            LoadChart(CurrentChart);
            Services.Gameplay.Audio.AudioTiming = currentTiming;
        }

        public void OpenProject(string path)
        {
            ProjectSettings project = DeserializeProject(path);
            isDirectFileProject = false;
            OpenProject(project, path, true);
        }

        public void OpenDirectFile(string path)
        {
            OpenUnsavedChangesDialog(() => OpenDirectFileImmediately(path));
        }

        private void OpenProject(ProjectSettings project, string path, bool rememberPath)
        {
            project.Path = path;
            CurrentProject = project;

            if (project.Charts.Count == 0)
            {
                throw new ComposeException(I18n.S("Compose.Exception.NoChartIncluded"));
            }

            CurrentChart = project.Charts[0];
            foreach (ChartSettings chart in project.Charts)
            {
                if (chart.ChartPath == project.LastOpenedChartPath)
                {
                    CurrentChart = chart;
                }
            }

            chartPicker.SetOptions(CurrentProject.Charts, CurrentChart);

            toggleInteractiveCanvas.interactable = true;
            noProjectLoadedHint.SetActive(false);

            currentChartPath.text = CurrentChart.ChartPath;
            LoadChart(CurrentChart);
            OnProjectLoad?.Invoke(CurrentProject);
            RecordRecentDirectory(path);
            if (rememberPath)
            {
                PlayerPrefs.SetString("LastProjectPath", path);
            }

            if (rememberPath)
            {
                Debug.Log(
                    I18n.S("Compose.Notify.Project.OpenProject", new Dictionary<string, object>()
                    {
                        { "Path", path },
                    }));
            }
            else
            {
                Debug.Log($"譜面ファイルを直接編集で開きました: \"{CurrentChart.ChartPath}\"");
            }
        }

        private void OpenDirectFileImmediately(string path)
        {
            if (Directory.Exists(path))
            {
                OpenDirectDirectory(Path.GetFullPath(path), null);
                return;
            }

            if (!File.Exists(path))
            {
                Services.Popups.Notify(Popups.Severity.Error, $"ファイルが見つかりません。\n{path}");
                return;
            }

            path = Path.GetFullPath(path);
            if (!DirectFileProjectResolver.IsSupportedDrop(path))
            {
                Services.Popups.Notify(Popups.Severity.Error, "AFF、OGG、JPG、または曲フォルダをドロップしてください。");
                return;
            }

            if (Path.GetExtension(path).Equals(".aff", StringComparison.OrdinalIgnoreCase))
            {
                if (DirectFileProjectResolver.ResolveAudioForChart(path) == null)
                {
                    Services.Popups.Notify(
                        Popups.Severity.Error,
                        $"譜面に対応するOGGが見つかりません。\n{Path.GetFileName(path)}");
                    return;
                }

                OpenDirectChart(path, null, null);
                return;
            }

            if (Path.GetExtension(path).Equals(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                OpenDirectDirectory(Path.GetDirectoryName(path), path);
                return;
            }

            string[] charts = DirectFileProjectResolver.FindChartsForAudio(path);
            if (charts.Length == 0)
            {
                OpenDirectChart(
                    Path.Combine(Path.GetDirectoryName(path), Values.DefaultChartFileName + ".aff"),
                    path,
                    null);
            }
            else if (charts.Length == 1)
            {
                OpenDirectChart(charts[0], path, null);
            }
            else
            {
                ShowDirectChartSelection(path, charts, path, null);
            }
        }

        private void OpenDirectDirectory(string directory, string droppedJacketPath)
        {
            string[] charts = DirectFileProjectResolver.FindLoadableChartsInDirectory(directory);
            if (charts.Length == 0)
            {
                string baseAudio = Path.Combine(directory, Values.BaseFileName + ".ogg");
                if (!File.Exists(baseAudio))
                {
                    Services.Popups.Notify(
                        Popups.Severity.Error,
                        $"曲フォルダに編集可能なAFFとOGG、またはbase.oggが見つかりません。\n{directory}");
                    return;
                }

                OpenDirectChart(
                    Path.Combine(directory, Values.DefaultChartFileName + ".aff"),
                    baseAudio,
                    droppedJacketPath);
            }
            else if (charts.Length == 1)
            {
                OpenDirectChart(charts[0], null, droppedJacketPath);
            }
            else
            {
                ShowDirectChartSelection(directory, charts, null, droppedJacketPath);
            }
        }

        private void ShowDirectChartSelection(
            string sourcePath,
            string[] chartPaths,
            string droppedAudioPath,
            string droppedJacketPath)
        {
            List<ButtonSetting> buttons = new List<ButtonSetting>();
            foreach (string chartPath in chartPaths)
            {
                string selectedChartPath = chartPath;
                buttons.Add(new ButtonSetting
                {
                    Text = Path.GetFileName(selectedChartPath),
                    Callback = () => OpenDirectChart(selectedChartPath, droppedAudioPath, droppedJacketPath),
                    ButtonColor = ButtonColor.Highlight,
                });
            }

            buttons.Add(new ButtonSetting
            {
                Text = "キャンセル",
                Callback = null,
                ButtonColor = ButtonColor.Default,
            });

            Services.Popups.CreateTextDialog(
                "譜面を選択",
                $"{GetSourceName(sourcePath)}で編集するAFFを選んでください。",
                buttons.ToArray());
        }

        private void OpenDirectChart(
            string selectedChartPath,
            string droppedAudioPath,
            string droppedJacketPath)
        {
            string directory = Path.GetDirectoryName(selectedChartPath);
            List<string> chartPaths = Directory.GetFiles(directory, "*.aff")
                .OrderBy(chart => Path.GetFileName(chart), StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!chartPaths.Contains(selectedChartPath, StringComparer.OrdinalIgnoreCase))
            {
                chartPaths.Add(selectedChartPath);
            }

            List<ChartSettings> charts = new List<ChartSettings>();
            foreach (string chartPath in chartPaths)
            {
                string audioPath = DirectFileProjectResolver.ResolveAudioForChart(chartPath);
                if (audioPath == null
                 && droppedAudioPath != null
                 && chartPath.Equals(selectedChartPath, StringComparison.OrdinalIgnoreCase))
                {
                    audioPath = droppedAudioPath;
                }

                if (audioPath == null)
                {
                    continue;
                }

                ChartSettings chart = new ChartSettings
                {
                    ChartPath = Path.GetFileName(chartPath),
                    AudioPath = Path.GetFileName(audioPath),
                    JacketPath = Path.GetFileName(
                        DirectFileProjectResolver.ResolveJacketForChart(chartPath, droppedJacketPath)),
                    BaseBpm = float.Parse(Values.DefaultBpm),
                };
                AutofillChart(chart);
                charts.Add(chart);
            }

            string selectedChartName = Path.GetFileName(selectedChartPath);
            if (!charts.Any(chart => chart.ChartPath.Equals(selectedChartName, StringComparison.OrdinalIgnoreCase)))
            {
                Services.Popups.Notify(Popups.Severity.Error, "選択した譜面を開くためのOGGが見つかりません。");
                return;
            }

            ProjectSettings directProject = new ProjectSettings
            {
                Path = Path.Combine(directory, ".ArcCreateNext.session.arcproj"),
                LastOpenedChartPath = selectedChartName,
                Charts = charts,
            };

            isDirectFileProject = true;
            OpenProject(directProject, directProject.Path, false);
        }

        private void OnOpenConfirmed()
        {
            void Open(string path)
            {
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                OpenProject(path);
            }

            string filterName = "ArcCreate Project";
            string title = "Open ArcCreate Project";
            string initPath = PlayerPrefs.GetString("LastProjectPath", "");
            string[] extensions = new string[] { Values.ProjectExtensionWithoutDot };
            if (Settings.UseNativeFileBrowser.Value)
            {
                string path = Shell.OpenFileDialog(
                    filterName: filterName,
                    extension: extensions,
                    title: title,
                    initPath: initPath);
                Open(path);
            }
            else
            {
                var filter = new SimpleFileBrowser.FileBrowser.Filter(
                    filterName,
                    extensions);
                SimpleFileBrowser.FileBrowser.SetFilters(false, extensions);
                SimpleFileBrowser.FileBrowser.ShowLoadDialog(
                    onSuccess: (string[] paths) => {
                        if (paths.Length >= 1)
                        {
                            Open(paths[0]);
                        }
                    },
                    onCancel: () => {},
                    pickMode: SimpleFileBrowser.FileBrowser.PickMode.Files,
                    allowMultiSelection: false,
                    initialPath: initPath,
                    title: title);
            }
        }

        private void OpenProjectFolder()
        {
            if (CurrentProject == null)
            {
                return;
            }

            Shell.OpenExplorer(Path.GetDirectoryName(CurrentProject.Path));
        }

        private void CreateRecentDirectoriesButton()
        {
            recentDirectoriesButton = Instantiate(openProjectButton, openProjectButton.transform.parent);
            recentDirectoriesButton.gameObject.name = "RecentDirectoriesButton";
            recentDirectoriesButton.transform.SetSiblingIndex(openProjectButton.transform.GetSiblingIndex() + 1);
            recentDirectoriesButton.onClick.RemoveAllListeners();

            IconText localizedLabel = recentDirectoriesButton.GetComponentInChildren<IconText>(true);
            TMP_Text label = recentDirectoriesButton.GetComponentInChildren<TMP_Text>(true);
            if (localizedLabel != null)
            {
                localizedLabel.enabled = false;
            }

            if (label != null)
            {
                label.text = "履歴";
            }

            Text icon = recentDirectoriesButton.GetComponentInChildren<Text>(true);
            if (icon != null)
            {
                icon.text = "\ue889";
            }

            RectTransform rect = recentDirectoriesButton.transform as RectTransform;
            if (rect != null && label != null)
            {
                rect.sizeDelta = new Vector2(label.preferredWidth + 40, rect.sizeDelta.y);
            }

            recentDirectoriesButton.onClick.AddListener(ShowRecentDirectories);
        }

        private void ShowRecentDirectories()
        {
            List<string> directories = LoadRecentDirectories();
            if (directories.Count == 0)
            {
                Services.Popups.Notify(Popups.Severity.Info, "読み込み履歴はまだありません。");
                return;
            }

            List<ButtonSetting> buttons = new List<ButtonSetting>();
            foreach (string directory in directories)
            {
                string selectedDirectory = directory;
                buttons.Add(new ButtonSetting
                {
                    Text = selectedDirectory,
                    Callback = () => OpenDirectFile(selectedDirectory),
                    ButtonColor = ButtonColor.Highlight,
                });
            }

            buttons.Add(new ButtonSetting
            {
                Text = "キャンセル",
                Callback = null,
                ButtonColor = ButtonColor.Default,
            });

            Services.Popups.CreateTextDialog(
                "読み込み履歴",
                "読み込む曲フォルダを選択してください。",
                buttons.ToArray());
        }

        private void RecordRecentDirectory(string sourcePath)
        {
            string directory = DirectFileProjectResolver.ResolveHistoryDirectory(sourcePath);
            if (directory == null)
            {
                return;
            }

            List<string> directories = LoadRecentDirectories();
            directories.RemoveAll(path => path.Equals(directory, StringComparison.OrdinalIgnoreCase));
            directories.Insert(0, directory);
            SaveRecentDirectories(directories.Take(MaxRecentDirectoryCount));
        }

        private List<string> LoadRecentDirectories()
        {
            string serialized = PlayerPrefs.GetString(RecentDirectoriesPlayerPrefKey, string.Empty);
            if (string.IsNullOrEmpty(serialized))
            {
                return new List<string>();
            }

            try
            {
                List<string> storedPaths = JsonConvert.DeserializeObject<List<string>>(serialized);
                List<string> directories = DirectFileProjectResolver
                    .NormalizeHistoryDirectories(storedPaths, MaxRecentDirectoryCount)
                    .ToList();
                SaveRecentDirectories(directories);
                return directories;
            }
            catch (JsonException exception)
            {
                Debug.LogWarning($"読み込み履歴を復元できなかったため初期化します。\n{exception.Message}");
                PlayerPrefs.DeleteKey(RecentDirectoriesPlayerPrefKey);
                return new List<string>();
            }
        }

        private static void SaveRecentDirectories(IEnumerable<string> directories)
        {
            PlayerPrefs.SetString(
                RecentDirectoriesPlayerPrefKey,
                JsonConvert.SerializeObject(directories));
        }

        private void Awake()
        {
            newProjectButton.gameObject.SetActive(false);
            CreateRecentDirectoriesButton();
            newProjectButton.onClick.AddListener(StartCreatingNewProject);
            openProjectButton.onClick.AddListener(StartOpeningProject);
            saveProjectButton.onClick.AddListener(SaveProject);
            openFolderButton.onClick.AddListener(OpenProjectFolder);
            toggleInteractiveCanvas.interactable = false;
            noProjectLoadedHint.SetActive(true);
            Settings.AutosaveInterval.OnValueChanged.AddListener(OnAutosaveIntervalChange);
            Settings.ShouldAutosave.OnValueChanged.AddListener(OnShouldAutosaveChange);
        }

        private void Start()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                windowsFileDrop = new WindowsFileDrop(paths => droppedFileBatches.Enqueue(paths));
                Debug.Log($"ファイルのドラッグ＆ドロップを初期化しました: 0x{windowsFileDrop.Window.ToInt64():X}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"ファイルのドラッグ＆ドロップを初期化できませんでした。\n{exception}");
            }
#endif
        }

        private void Update()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            while (droppedFileBatches.TryDequeue(out IReadOnlyList<string> paths))
            {
                Debug.Log($"ファイルドロップメッセージを受信しました: {paths.Count}件");
                if (paths.Count == 0)
                {
                    continue;
                }

                if (paths.Count > 1)
                {
                    Services.Popups.Notify(Popups.Severity.Warning, "一度に開けるファイルは1個です。先頭のファイルを開きます。");
                }

                Debug.Log($"ファイルをドロップしました: \"{paths[0]}\"");
                OpenDirectFile(paths[0]);
            }
#endif
        }

        private void OnDestroy()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            windowsFileDrop?.Dispose();
#endif
            if (pendingAudioSwitch != null)
            {
                gameplayData.AudioClip.OnValueChange -= pendingAudioSwitch;
                pendingAudioSwitch = null;
            }

            newProjectButton.onClick.RemoveListener(StartCreatingNewProject);
            openProjectButton.onClick.RemoveListener(StartOpeningProject);
            recentDirectoriesButton?.onClick.RemoveListener(ShowRecentDirectories);
            saveProjectButton.onClick.RemoveListener(SaveProject);
            openFolderButton.onClick.RemoveListener(OpenProjectFolder);
            Settings.AutosaveInterval.OnValueChanged.RemoveListener(OnAutosaveIntervalChange);
            Settings.ShouldAutosave.OnValueChanged.RemoveListener(OnShouldAutosaveChange);
        }

        private void OnShouldAutosaveChange(bool arg0) => OnAutosaveIntervalChange(Settings.AutosaveInterval.Value);

        private void OnAutosaveIntervalChange(int value)
        {
            autosaveHelper?.Dispose();
            autosaveHelper = null;
            if (Settings.ShouldAutosave.Value)
            {
                autosaveHelper = new AutosaveHelper(this, value);
            }
        }

        private void SerializeProject(ProjectSettings projectSettings)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(new CamelCaseNamingConvention())
                .Build();
            string yaml = serializer.Serialize(projectSettings);
            File.WriteAllText(projectSettings.Path, yaml);

            Values.ProjectModified = false;

            Debug.Log(
                I18n.S("Compose.Notify.Project.SaveProject", new Dictionary<string, object>()
                {
                    { "Path", projectSettings.Path },
                }));
        }

        private ProjectSettings DeserializeProject(string path)
        {
            try
            {
                string content = File.ReadAllText(path);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(new CamelCaseNamingConvention())
                    .IgnoreUnmatchedProperties()
                    .Build();
                return deserializer.Deserialize<ProjectSettings>(content);
            }
            catch (Exception e)
            {
                throw new ComposeException(I18n.S("Compose.Exception.LoadProject", new Dictionary<string, object>
                {
                    { "Path", path },
                    { "Error", e.Message },
                }));
            }
        }

        private void AutofillChart(ChartSettings chart)
        {
            chart.Title = string.IsNullOrEmpty(chart.Title) ? Values.DefaultTitle : chart.Title;
            chart.Composer = string.IsNullOrEmpty(chart.Composer) ? Values.DefaultComposer : chart.Composer;
            chart.SyncBaseBpm = true;

            switch (chart.ChartPath.Split('.')[0])
            {
                case "0":
                    chart.DifficultyColor = defaultDifficultyColors[0].ConvertToHexCode();
                    chart.Difficulty = defaultDifficultyNames[0];
                    break;
                case "1":
                    chart.DifficultyColor = defaultDifficultyColors[1].ConvertToHexCode();
                    chart.Difficulty = defaultDifficultyNames[1];
                    break;
                case "2":
                    chart.DifficultyColor = defaultDifficultyColors[2].ConvertToHexCode();
                    chart.Difficulty = defaultDifficultyNames[2];
                    break;
                case "3":
                    chart.DifficultyColor = defaultDifficultyColors[3].ConvertToHexCode();
                    chart.Difficulty = defaultDifficultyNames[3];
                    break;
                case "4":
                    chart.DifficultyColor = defaultDifficultyColors[4].ConvertToHexCode();
                    chart.Difficulty = defaultDifficultyNames[4];
                    break;
                default:
                    chart.DifficultyColor = defaultDifficultyColors[2].ConvertToHexCode();
                    chart.Difficulty = defaultDifficultyNames[2];
                    break;
            }
        }

        private void LoadChart(
            ChartSettings chart,
            bool preserveAudioTiming = false,
            bool playNewAudioFromStart = false)
        {
            string dir = Path.GetDirectoryName(CurrentProject.Path);
            string path = Path.Combine(dir, chart.ChartPath);

            if (!File.Exists(path))
            {
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                }

                var writer = ChartFileWriterFactory.GetWriterFromFilename(path);
                using (FileStream fileStream = File.OpenWrite(path))
                {
                    writer.Write(
                        new StreamWriter(fileStream),
                        0,
                        1,
                        new List<(RawTimingGroup, IEnumerable<RawEvent>)>()
                        {
                            (new RawTimingGroup(), new List<RawEvent>()
                                {
                                    new RawTiming()
                                    {
                                        Type = RawEventType.Timing,
                                        Timing = 0,
                                        TimingGroup = 0,
                                        Bpm = chart.BaseBpm,
                                        Divisor = 4,
                                    },
                                }),
                        });
                }
            }

            rawEditor.LoadFromPath(path);

            ChartReader reader = ChartReaderFactory.GetReader(new PhysicalFileAccess(), path);
            Result<ChartFileErrors> parseResult = reader.Parse();

            if (parseResult.IsOk)
            {
                if (playNewAudioFromStart)
                {
                    RegisterAudioSwitchPlayback();
                }

                gameplayData.LoadChart(
                    reader,
                    Path.GetDirectoryName(path),
                    resetAudioTiming: !preserveAudioTiming);
            }
            else
            {
                Services.Popups.CreateTextDialog(
                    title: I18n.S("Compose.Dialog.LoadChartError.Title"),
                    content: I18n.S("Compose.Dialog.LoadChartError.Content", new Dictionary<string, object>
                    {
                        { "ChartPath", path },
                        { "TabName", I18n.S("Compose.UI.PanelNames.RawEditor") },
                        { "Content", parseResult.Error.Message },
                    }),
                    new ButtonSetting
                    {
                        Text = I18n.S("Compose.Dialog.LoadChartError.Confirm"),
                        Callback = null,
                        ButtonColor = ButtonColor.Highlight,
                    });
            }

            OnChartLoad?.Invoke(chart);
            Values.ProjectModified = false;

            Debug.Log(
                I18n.S("Compose.Notify.Project.OpenChart", new Dictionary<string, object>()
                {
                    { "Path", chart.ChartPath },
                }));

            autosaveHelper?.Dispose();
            autosaveHelper = null;
            if (Settings.ShouldAutosave.Value)
            {
                autosaveHelper = new AutosaveHelper(this, Settings.AutosaveInterval.Value);
            }
        }

        private void SerializeChart(ProjectSettings projectSettings)
        {
            string dir = Path.Combine(Path.GetDirectoryName(projectSettings.Path), Path.GetDirectoryName(CurrentChart.ChartPath));
            var chartData = new RawEventsBuilder().GetEvents();
            new ChartSerializer(new PhysicalFileAccess(), dir).Write(
                gameplayData.AudioOffset.Value,
                gameplayData.TimingPointDensityFactor.Value,
                chartData);

            if (Settings.ShouldBackup.Value)
            {
                new BackupHelper(projectSettings.Path).Serialize(
                    gameplayData.AudioOffset.Value,
                    gameplayData.TimingPointDensityFactor.Value,
                    chartData);
            }

            string scJson = Services.Gameplay.Scenecontrol.Export();
            if (!string.IsNullOrEmpty(scJson) && scJson != "[]")
            {
                string scPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(CurrentChart.ChartPath) + ".sc.json");
                File.WriteAllText(scPath, scJson);
            }
        }

        private void OpenUnsavedChangesDialog(Action onConfirm)
        {
            if (Values.ProjectModified == false)
            {
                onConfirm.Invoke();
                return;
            }

            Services.Popups.CreateTextDialog(
                title: I18n.S("Compose.Dialog.UnsavedChanges.Title"),
                content: I18n.S("Compose.Dialog.UnsavedChanges.Content"),
                buttonSettings: new ButtonSetting[]
                {
                    new ButtonSetting
                    {
                        Text = I18n.S("Compose.Dialog.UnsavedChanges.Yes"),
                        Callback = () =>
                        {
                            Services.Project.SaveProject();
                            onConfirm.Invoke();
                            Values.ProjectModified = false;
                        },
                        ButtonColor = ButtonColor.Highlight,
                    },
                    new ButtonSetting
                    {
                        Text = I18n.S("Compose.Dialog.UnsavedChanges.No"),
                        Callback = () =>
                        {
                            onConfirm.Invoke();
                            Values.ProjectModified = false;
                        },
                        ButtonColor = ButtonColor.Danger,
                    },
                    new ButtonSetting
                    {
                        Text = I18n.S("Compose.Dialog.UnsavedChanges.Cancel"),
                        Callback = null,
                        ButtonColor = ButtonColor.Default,
                    },
                });
        }

        private string GetAbsoluteAudioPath(ChartSettings chart)
        {
            if (chart == null || string.IsNullOrEmpty(chart.AudioPath) || CurrentProject == null)
            {
                return null;
            }

            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(CurrentProject.Path), chart.AudioPath));
        }

        private static string GetSourceName(string sourcePath)
        {
            return Directory.Exists(sourcePath)
                ? new DirectoryInfo(sourcePath).Name
                : Path.GetFileName(sourcePath);
        }

        private void RegisterAudioSwitchPlayback()
        {
            if (pendingAudioSwitch != null)
            {
                gameplayData.AudioClip.OnValueChange -= pendingAudioSwitch;
            }

            pendingAudioSwitch = clip =>
            {
                gameplayData.AudioClip.OnValueChange -= pendingAudioSwitch;
                pendingAudioSwitch = null;
                Services.Gameplay.Audio.PlayImmediately(0);
            };
            gameplayData.AudioClip.OnValueChange += pendingAudioSwitch;
        }
    }
}
