using ArcCreate.Gameplay.Audio;
using ArcCreate.Gameplay.Chart;
using ArcCreate.Gameplay.GameplayCamera;
using ArcCreate.Gameplay.Hitsound;
using ArcCreate.Gameplay.InputFeedback;
using ArcCreate.Gameplay.Judgement;
using ArcCreate.Gameplay.Particle;
using ArcCreate.Gameplay.Render;
using ArcCreate.Gameplay.Scenecontrol;
using ArcCreate.Gameplay.Score;
using ArcCreate.Gameplay.Skin;
using UnityEngine;
using UnityEngine.Serialization;

namespace ArcCreate.Gameplay
{
    internal class Services : MonoBehaviour
    {
        [SerializeField] private SkinService skin;
        [FormerlySerializedAs("audio")]
        [SerializeField] private AudioService audioService;
        [FormerlySerializedAs("camera")]
        [SerializeField] private CameraService cameraService;
        [SerializeField] private ChartService chart;
        [SerializeField] private ParticleService particle;
        [SerializeField] private JudgementService judgement;
        [SerializeField] private InputFeedbackService inputFeedback;
        [SerializeField] private ScoreService score;
        [SerializeField] private ScenecontrolService scenecontrol;
        [SerializeField] private RenderService render;
        [SerializeField] private HitsoundService hitsound;

        public static ISkinService Skin { get; private set; }

        public static IChartService Chart { get; private set; }

        public static ICameraService Camera { get; private set; }

        public static IAudioService Audio { get; private set; }

        public static IParticleService Particle { get; private set; }

        public static IJudgementService Judgement { get; private set; }

        public static IInputFeedbackService InputFeedback { get; private set; }

        public static IScenecontrolService Scenecontrol { get; private set; }

        public static IScoreService Score { get; private set; }

        public static IRenderService Render { get; private set; }

        public static IHitsoundService Hitsound { get; private set; }

        private void Awake()
        {
            Skin = skin;
            Chart = chart;
            Particle = particle;
            Judgement = judgement;
            Audio = audioService;
            Score = score;
            InputFeedback = inputFeedback;
            Scenecontrol = scenecontrol;
            Camera = cameraService;
            Render = render;
            Hitsound = hitsound;
        }
    }
}