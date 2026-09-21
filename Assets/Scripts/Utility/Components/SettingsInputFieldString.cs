using TMPro;
using UnityEngine;

namespace ArcCreate
{
    [RequireComponent(typeof(TMP_InputField))]
    public class SettingsInputFieldString : MonoBehaviour
    {
        private TMP_InputField input;
        private StringSetting setting;

        private TMP_InputField Input
        {
            get
            {
                input = input == null ? GetComponent<TMP_InputField>() : input;
                return input;
            }
        }

        public void Setup(StringSetting setting)
        {
            this.setting = setting;
            setting.OnValueChanged.AddListener(OnSettingChange);
            OnSettingChange(setting.Value);
        }

        private void Awake()
        {
            Input.onEndEdit.AddListener(OnUIChange);
        }

        private void OnDestroy()
        {
            Input.onEndEdit.RemoveListener(OnUIChange);
            setting?.OnValueChanged.RemoveListener(OnSettingChange);
        }

        private void OnSettingChange(string value)
        {
            Input.SetTextWithoutNotify(value);
        }

        private void OnUIChange(string value)
        {
            setting.Value = value.Trim();
            Input.SetTextWithoutNotify(setting.Value);
        }
    }
}
