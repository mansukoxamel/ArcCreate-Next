using System;
using System.IO;
using ArcCreate.Data;
using ArcCreate.Gameplay;
using ArcCreate.Utility;
using ArcCreate.Utility.Parser;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcCreate.Compose.Components
{
    public class SettingFields : MonoBehaviour
    {
        [SerializeField] private GameplayData gameplayData;

        [Header("Common")]
        [SerializeField] private TMP_InputField playbackSpeedField;
        [SerializeField] private TMP_InputField densityField;
        [SerializeField] private TimingGroupField groupField;
        [SerializeField] private SettingsDropdown inputModeDropdown;

        [Header("Gameplay")]
        [SerializeField] private TMP_InputField speedField;
        [SerializeField] private TMP_Dropdown aspectRatioDropdown;
        [SerializeField] private SettingsDropdown indicatorPositionDropdown;
        [SerializeField] private SettingsToggle maxIndicatorToggle;
        [SerializeField] private SettingsToggle colorblindModeToggle;
        [SerializeField] private SettingsToggle classicArcParticleToggle;
        [SerializeField] private SettingsDropdown scoreDisplayDropdown;

        [Header("Judgement")]
        [SerializeField] private SettingsDropdown lateEarlyPositionDropdown;
        [SerializeField] private SettingsToggle showMsDifferenceToggle;
        [SerializeField] private SettingsToggle showMaxToggle;
        [SerializeField] private SettingsToggle showPerfectToggle;
        [SerializeField] private SettingsToggle showGoodToggle;
        [SerializeField] private SettingsToggle showMissToggle;

        [Header("Audio")]
        [SerializeField] private SettingsInputFieldFloat musicAudioField;
        [SerializeField] private SettingsInputFieldFloat effectAudioField;
        [SerializeField] private SettingsInputFieldInteger globalOffsetField;
        [SerializeField] private SettingsToggle syncToDspTime;

        [Header("Display")]
        [SerializeField] private SettingsInputFieldInteger framerateField;
        [SerializeField] private SettingsToggle vsyncField;
        [SerializeField] private SettingsToggle showFramerateToggle;

        [Header("Input")]
        [SerializeField] private Button reloadHotkeysButton;
        [SerializeField] private Button openHotkeySettingsButton;
        [SerializeField] private SettingsToggle showHotkeyHintsToggle;
        [SerializeField] private SettingsToggle useNativeFileBrowserToggle;
        [SerializeField] private SettingsToggle allowCreatingNotesBackwardToggle;
        [SerializeField] private SettingsToggle blockCreatingOverlappedNotesToggle;
        [SerializeField] private SettingsToggle enableEditingArctapWidthToggle;
        [SerializeField] private SettingsToggle snapFloorNoteWithGrid;
        [SerializeField] private SettingsInputFieldFloat scrollVerticalField;
        [SerializeField] private SettingsInputFieldFloat scrollHorizontalField;
        [SerializeField] private SettingsInputFieldFloat scrollTimelineField;
        [SerializeField] private SettingsInputFieldFloat trackThresholdField;
        [SerializeField] private SettingsInputFieldInteger trackMaxTimingField;

        [Header("Saving")]
        [SerializeField] private SettingsToggle shouldAutosave;
        [SerializeField] private SettingsInputFieldInteger autosaveInterval;
        [SerializeField] private TMP_InputField autosaveIntervalObject;
        [SerializeField] private SettingsToggle shouldSaveBackup;
        [SerializeField] private SettingsInputFieldInteger backupCount;
        [SerializeField] private TMP_InputField backupCountObject;

        [Header("Credits")]
        [SerializeField] private Button openCreditsButton;
        [SerializeField] private Dialog creditsDialog;

        private TMP_InputField timelineNoteSpeedField;

        private void Awake()
        {
            SetupTimelineNoteSpeedField();
            SetupSongFolderField();
            aspectRatioDropdown.onValueChanged.AddListener(OnAspectRatioDropdown);
            reloadHotkeysButton.onClick.AddListener(OnReloadHotkeysButton);
            openHotkeySettingsButton.onClick.AddListener(OnOpenHotkeySettingsButton);
            densityField.onEndEdit.AddListener(OnDensityField);
            openCreditsButton.onClick.AddListener(creditsDialog.Open);
            playbackSpeedField.onEndEdit.AddListener(OnPlaybackSpeedField);
            timelineNoteSpeedField.onEndEdit.AddListener(OnSpeedField);
            speedField.onEndEdit.AddListener(OnSpeedField);

            Settings.ViewportAspectRatioSetting.OnValueChanged.AddListener(OnAspectRatioSetting);
            Settings.ShouldAutosave.OnValueChanged.AddListener(OnAutosaveSetting);
            Settings.ShouldBackup.OnValueChanged.AddListener(OnBackupSetting);
            Settings.DropRate.OnValueChanged.AddListener(OnDropRateSetting);
            gameplayData.PlaybackSpeed.OnValueChange += OnPlaybackSpeedGameplayChange;
            Values.BeatlineDensity.OnValueChange += OnDensity;

            OnAspectRatioSetting(Settings.ViewportAspectRatioSetting.Value);
            OnAutosaveSetting(Settings.ShouldAutosave.Value);
            OnBackupSetting(Settings.ShouldBackup.Value);
            OnDropRateSetting(Settings.DropRate.Value);
            OnPlaybackSpeedGameplayChange(gameplayData.PlaybackSpeed.Value);
            OnDensity(Values.BeatlineDensity.Value);

            musicAudioField.Setup(Settings.MusicAudio, 2);
            effectAudioField.Setup(Settings.EffectAudio, 2);
            globalOffsetField.Setup(Settings.GlobalAudioOffset);
            framerateField.Setup(Settings.Framerate);
            vsyncField.Setup(Settings.VSync);
            showFramerateToggle.Setup(Settings.ShowFPSCounter);
            scrollVerticalField.Setup(Settings.ScrollSensitivityVertical, 2);
            scrollHorizontalField.Setup(Settings.ScrollSensitivityHorizontal, 2);
            scrollTimelineField.Setup(Settings.ScrollSensitivityTimeline, 2);
            trackThresholdField.Setup(Settings.TrackScrollThreshold, 2);
            trackMaxTimingField.Setup(Settings.TrackScrollMaxMovement);
            maxIndicatorToggle.Setup(Settings.EnableMaxIndicator);
            shouldAutosave.Setup(Settings.ShouldAutosave);
            autosaveInterval.Setup(Settings.AutosaveInterval);
            shouldSaveBackup.Setup(Settings.ShouldBackup);
            backupCount.Setup(Settings.BackupCount);
            syncToDspTime.Setup(Settings.SyncToDSPTime);
            inputModeDropdown.Setup(Settings.InputMode, typeof(InputMode), "Compose.UI.Top.Label.InputModeOptions");
            indicatorPositionDropdown.Setup(Settings.FrPmIndicatorPosition, typeof(FrPmPosition), "Gameplay.Selection.Settings.FrPmPosition");
            showHotkeyHintsToggle.Setup(Settings.EnableKeybindHintDisplay);
            useNativeFileBrowserToggle.Setup(Settings.UseNativeFileBrowser);
            allowCreatingNotesBackwardToggle.Setup(Settings.AllowCreatingNotesBackward);
            enableEditingArctapWidthToggle.Setup(Settings.EnableArctapWidthEditing);
            snapFloorNoteWithGrid.Setup(Settings.SnapFloorNoteWithGrid);
            blockCreatingOverlappedNotesToggle.Setup(Settings.BlockOverlapNoteCreation);
            colorblindModeToggle.Setup(Settings.EnableColorblind);
            classicArcParticleToggle.Setup(Settings.ClassicArcParticle);
            scoreDisplayDropdown.Setup(Settings.ScoreDisplayMode, typeof(ScoreDisplayMode), "Gameplay.Selection.Settings.ScoreDisplay");
            lateEarlyPositionDropdown.Setup(Settings.LateEarlyTextPosition, typeof(EarlyLateTextPosition), "Gameplay.Selection.Settings.EarlyLateTextPosition");
            showMsDifferenceToggle.Setup(Settings.DisplayMsDifference);
            showMaxToggle.Setup(Settings.ShowMaxJudgement);
            showPerfectToggle.Setup(Settings.ShowPerfectJudgement);
            showGoodToggle.Setup(Settings.ShowGoodJudgement);
            showMissToggle.Setup(Settings.ShowMissJudgement);
        }

        private void OnDestroy()
        {
            aspectRatioDropdown.onValueChanged.RemoveListener(OnAspectRatioDropdown);
            reloadHotkeysButton.onClick.RemoveListener(OnReloadHotkeysButton);
            openHotkeySettingsButton.onClick.RemoveListener(OnOpenHotkeySettingsButton);
            densityField.onEndEdit.RemoveListener(OnDensityField);
            openCreditsButton.onClick.RemoveListener(creditsDialog.Open);
            playbackSpeedField.onEndEdit.RemoveListener(OnPlaybackSpeedField);
            timelineNoteSpeedField.onEndEdit.RemoveListener(OnSpeedField);
            speedField.onEndEdit.RemoveListener(OnSpeedField);

            Settings.ViewportAspectRatioSetting.OnValueChanged.RemoveListener(OnAspectRatioSetting);
            Settings.ShouldAutosave.OnValueChanged.RemoveListener(OnAutosaveSetting);
            Settings.ShouldBackup.OnValueChanged.RemoveListener(OnBackupSetting);
            Settings.DropRate.OnValueChanged.RemoveListener(OnDropRateSetting);
            gameplayData.PlaybackSpeed.OnValueChange -= OnPlaybackSpeedGameplayChange;
            Values.BeatlineDensity.OnValueChange -= OnDensity;
        }

        private void OnReloadHotkeysButton()
        {
            Services.Navigation.ReloadHotkeys();
        }

        private void OnOpenHotkeySettingsButton()
        {
            Shell.OpenExplorer(Path.GetDirectoryName(Services.Navigation.ConfigFilePath));
        }

        private void OnPlaybackSpeedField(string str)
        {
            if (Evaluator.TryFloat(str, out float val))
            {
                val = Mathf.Max(val, 0.1f);
                gameplayData.PlaybackSpeed.Value = val;
            }

            playbackSpeedField.SetTextWithoutNotify(gameplayData.PlaybackSpeed.Value.ToString());
        }

        private void OnPlaybackSpeedGameplayChange(float obj)
        {
            playbackSpeedField.SetTextWithoutNotify(gameplayData.PlaybackSpeed.Value.ToString());
        }

        private void OnAspectRatioDropdown(int value)
        {
            Settings.ViewportAspectRatioSetting.Value = value;
        }

        private void OnAspectRatioSetting(int value)
        {
            aspectRatioDropdown.value = value;
        }

        private void OnSpeedField(string value)
        {
            if (Evaluator.TryFloat(value, out float speed))
            {
                float dropRate = Mathf.Clamp(
                    speed * Constants.DropRateScalar,
                    Constants.MinDropRate,
                    Constants.MaxDropRate);
                Settings.DropRate.Value = (int)System.Math.Round(dropRate);
            }

            speedField.SetTextWithoutNotify((Settings.DropRate.Value / Constants.DropRateScalar).ToString());
        }

        private void OnDropRateSetting(int value)
        {
            string text = (value / Constants.DropRateScalar).ToString("F1");
            speedField.SetTextWithoutNotify(text);
            timelineNoteSpeedField.SetTextWithoutNotify(text);
        }

        // A text field for the folder that holds the song folders. It is made from the number field of the backup count
        // (the same way as the note speed field of the timeline), so the scene does not need to change.
        private void SetupSongFolderField()
        {
            GameObject folderObject = Instantiate(backupCountObject.gameObject, backupCountObject.transform.parent);
            folderObject.name = "SongFolder";
            folderObject.transform.SetSiblingIndex(backupCountObject.transform.GetSiblingIndex() + 1);

            // The rows of the file section are placed by hand (each row 50 below the last one), so the copy is put under
            // the backup count, and the section is made taller by the same amount.
            const float RowPitch = 50f;
            RectTransform folderRect = folderObject.transform as RectTransform;
            folderRect.anchoredPosition += new Vector2(0f, -RowPitch);
            RectTransform section = folderRect.parent as RectTransform;
            section.sizeDelta += new Vector2(0f, RowPitch);

            foreach (SettingsInputFieldInteger integerField in folderObject.GetComponents<SettingsInputFieldInteger>())
            {
                Destroy(integerField);
            }

            foreach (NumberInputField numberField in folderObject.GetComponents<NumberInputField>())
            {
                Destroy(numberField);
            }

            foreach (Button button in folderObject.GetComponentsInChildren<Button>(true))
            {
                button.gameObject.SetActive(false);
            }

            TMP_InputField input = folderObject.GetComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.Standard;
            input.characterValidation = TMP_InputField.CharacterValidation.None;
            input.interactable = true;
            folderObject.AddComponent<SettingsInputFieldString>().Setup(Settings.SongFolderRoot);

            // The row label is the text object "Label" of the copied field. It still says "最大数" (the backup count),
            // so it is replaced. Nothing else in the copy may keep that text.
            Transform labelTransform = folderObject.transform.Find("Label");
            if (labelTransform == null)
            {
                throw new InvalidOperationException("設定の行の複製に、Labelがありません。バックアップ数の欄の構造が変わっています。");
            }

            I18nText labelI18n = labelTransform.GetComponent<I18nText>();
            if (labelI18n != null)
            {
                labelI18n.enabled = false;
            }

            labelTransform.GetComponent<TMP_Text>().text = I18n.S("Compose.UI.Settings.Label.Files.SongFolder");
        }

        private void SetupTimelineNoteSpeedField()
        {
            GameObject noteSpeedObject = Instantiate(playbackSpeedField.gameObject, playbackSpeedField.transform.parent);
            noteSpeedObject.name = "NoteSpeed";
            noteSpeedObject.transform.SetSiblingIndex(playbackSpeedField.transform.GetSiblingIndex() + 1);
            timelineNoteSpeedField = noteSpeedObject.GetComponent<TMP_InputField>();
            noteSpeedObject.GetComponent<NumberInputField>().SetIncrement(0.1f);
            I18nText label = noteSpeedObject.GetComponentInChildren<I18nText>(true);
            label.enabled = false;
            label.LoadComponent();
            label.Text.text = I18n.S("Compose.UI.Timeline.Label.NoteSpeed");

            SetTimelineSlot(playbackSpeedField.transform as RectTransform, 0f, 0.25f);
            SetTimelineSlot(noteSpeedObject.transform as RectTransform, 0.25f, 0.5f);
            SetTimelineSlot(densityField.transform as RectTransform, 0.5f, 0.75f);
            SetTimelineSlot(groupField.transform as RectTransform, 0.75f, 1f);
        }

        private static void SetTimelineSlot(RectTransform rect, float minX, float maxX)
        {
            rect.anchorMin = new Vector2(minX, rect.anchorMin.y);
            rect.anchorMax = new Vector2(maxX, rect.anchorMax.y);
        }

        private void OnDensityField(string value)
        {
            // if there's ever a command interface then this will be moved there
            if (EasterEggs.TryTrigger(value))
            {
                densityField.SetTextWithoutNotify(Values.BeatlineDensity.Value.ToString());
                return;
            }

            if (Evaluator.TryFloat(value, out float density))
            {
                Values.BeatlineDensity.Value = density;
            }

            densityField.SetTextWithoutNotify(Values.BeatlineDensity.Value.ToString());
        }

        private void OnDensity(float density)
        {
            densityField.SetTextWithoutNotify(Values.BeatlineDensity.Value.ToString());
        }

        private void OnBackupSetting(bool on)
        {
            backupCountObject.interactable = on;
        }

        private void OnAutosaveSetting(bool on)
        {
            autosaveIntervalObject.interactable = on;
        }
    }
}
