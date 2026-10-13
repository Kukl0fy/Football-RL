using UnityEngine;

namespace FootballRL.Input
{
    /// <summary>Wybór siły i kąta strzału (klawiatura ręczna).</summary>
    public class ManualKickInputState
    {
        public const float DefaultAngleDeg = 0f;
        public const float DefaultKick = 1f;
        public const float AngleStepDeg = 5f;
        public const float KickStep = 0.05f;
        public const float MinKick = 0f;
        public const float MaxKick = 1f;

        float _selectedAngleDeg = DefaultAngleDeg;
        float _selectedKick = DefaultKick;

        public float SelectedAngleDeg => _selectedAngleDeg;
        public float SelectedKick => _selectedKick;

        public float SelectedLoft => _selectedAngleDeg / FootballRL.Gameplay.KickConfig.LoftScaleDeg - 1f;

        public void ResetToDefaults()
        {
            _selectedAngleDeg = DefaultAngleDeg;
            _selectedKick = DefaultKick;
        }

        public void AdjustAngle(float deltaDeg)
        {
            _selectedAngleDeg = Mathf.Clamp(_selectedAngleDeg + deltaDeg, 0f, 55f);
        }

        public void AdjustKick(float delta)
        {
            _selectedKick = Mathf.Clamp(_selectedKick + delta, MinKick, MaxKick);
        }

        public void SetLoft(float loft)
        {
            _selectedAngleDeg = Mathf.Clamp((loft + 1f) * FootballRL.Gameplay.KickConfig.LoftScaleDeg, 0f, 55f);
        }
    }
}
