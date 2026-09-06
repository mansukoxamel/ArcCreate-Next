using System.Linq;
using System.Reflection;
using ArcCreate.Compose.Popups;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ArcCreate.Tests
{
    public class TextDialogTest
    {
        [Test]
        public void SetupVerticalList_DisplaysWindowsPathsLiterallyOnDarkRows()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Editor/Dialogs/TextDialog.prefab");
            Assert.That(prefab, Is.Not.Null);

            GameObject instance = Object.Instantiate(prefab);
            TextDialog dialog = instance.GetComponent<TextDialog>();
            const string path = @"D:\work\arcaea\songs\trappola";

            try
            {
                typeof(TextDialog)
                    .GetField("rect", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(dialog, instance.GetComponent<RectTransform>());
                dialog.SetupVerticalList(
                    "読み込み履歴",
                    "読み込む曲フォルダを選択してください。",
                    new[]
                    {
                        new ButtonSetting
                        {
                            Text = path,
                            Callback = () => { },
                            ButtonColor = ButtonColor.Default,
                        },
                    });

                TextDialogButton row = instance.GetComponentInChildren<TextDialogButton>(true);
                TMP_Text text = row.GetComponentInChildren<TMP_Text>(true);
                Image background = row.GetComponents<Image>().Single();

                Assert.That(text.text, Is.EqualTo(path));
                Assert.That(text.parseCtrlCharacters, Is.False);
                Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
                Assert.That(background.color.r, Is.EqualTo(background.color.g).Within(0.001f));
                Assert.That(background.color.g, Is.EqualTo(background.color.b).Within(0.001f));
                Assert.That(background.color.r, Is.LessThan(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
