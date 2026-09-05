using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcCreate.SceneTransition
{
    public class TitleSceneManager : MonoBehaviour
    {
        public async void LoadPlayer()
        {
            print("Loading Player...");
            _ = SceneManager.UnloadSceneAsync(1);
            await SceneTransitionManager.Instance.LoadSceneAdditive(SceneNames.SelectScene);
        }

        public async void LoadRemote()
        {
            print("Loading Remote...");
            _ = SceneManager.UnloadSceneAsync(1);
            await SceneTransitionManager.Instance.LoadSceneAdditive(SceneNames.RemoteScene);
        }
    }
}
