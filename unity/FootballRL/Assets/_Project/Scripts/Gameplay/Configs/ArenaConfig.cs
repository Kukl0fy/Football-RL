using UnityEngine;
using UnityEngine.Serialization;

namespace FootballRL.Gameplay
{
    /// <summary>Wymiary areny i bramek (1 jednostka = 1 m). Oś gry: ±Z; linia bramkowa: ±X.</summary>
    [CreateAssetMenu(fileName = "ArenaConfig", menuName = "FootballRL/Arena Config")]
    public class ArenaConfig : ScriptableObject
    {
        [Header("Boisko — oś gry ±Z (między liniami bramkowymi)")]
        [FormerlySerializedAs("pitchLengthX")]
        public float pitchPlayLengthZ = 20f;

        [Header("Boisko — linia bramkowa ±X (krótki bok z otworem)")]
        [FormerlySerializedAs("pitchWidthZ")]
        public float pitchWidthX = 12f;

        [Header("Bramka (krótkie boki ±Z)")]
        public float goalWidth = 3f;
        public float goalHeight = 2f;
        public float goalDepth = 1f;

        [Header("Piłka")]
        public float ballRadius = 0.22f;

        [Header("Ściany")]
        public float wallHeight = 2.5f;
        public float wallThickness = 0.2f;

        [Header("Awaryjna dolna granica (pod G4 BallOut)")]
        public float emergencyFloorY = -2f;
        public float emergencyFloorDepth = 0.5f;

        [Header("Kolory (proste materiały)")]
        public Color pitchColor = new Color(0.18f, 0.55f, 0.22f);
        public Color wallColor = new Color(0.75f, 0.75f, 0.78f);
        public Color goalFrameColor = new Color(0.95f, 0.95f, 0.95f);
        public Color ballColor = new Color(0.95f, 0.92f, 0.15f);

        public float HalfPlayLengthZ => pitchPlayLengthZ * 0.5f;
        public float HalfWidthX => pitchWidthX * 0.5f;
        public float HalfGoalWidth => goalWidth * 0.5f;
    }
}
