using UnityEngine;
using UnityEngine.InputSystem;

namespace FootballRL.Presentation
{
    /// <summary>
    /// Obraca kamerę wokół areny. Nie używa klawiszy ruchu ani kopnięcia.
    /// Prawy przycisk myszy — obrót, kółko — zoom, środkowy — przesuwanie.
    /// </summary>
    public class ArenaOrbitCamera : MonoBehaviour
    {
        Transform _focus;
        Vector3 _pan;
        float _yaw;
        float _pitch = 42f;
        float _distance = 22f;
        bool _ready;

        public void SetFocus(Transform focus)
        {
            _focus = focus;
            _pan = Vector3.zero;
            SnapFromCurrentTransform();
        }

        void LateUpdate()
        {
            if (!_ready)
                SnapFromCurrentTransform();

            ReadMouse();
            ApplyPose();
        }

        void SnapFromCurrentTransform()
        {
            var focus = FocusPoint;
            var offset = transform.position - focus;
            _distance = Mathf.Clamp(offset.magnitude, 4f, 45f);
            if (_distance < 0.5f)
                _distance = 22f;

            _yaw = Mathf.Atan2(offset.x, -offset.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Asin(Mathf.Clamp(offset.y / _distance, -1f, 1f)) * Mathf.Rad2Deg;
            _pitch = Mathf.Clamp(_pitch, 8f, 80f);
            _ready = true;
        }

        void ReadMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                _yaw += delta.x * 0.15f;
                _pitch = Mathf.Clamp(_pitch - delta.y * 0.12f, 8f, 80f);
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                _distance = Mathf.Clamp(_distance - scroll * 0.02f, 4f, 48f);

            if (mouse.middleButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                var scale = _distance * 0.0022f;
                _pan += (-transform.right * delta.x - transform.up * delta.y) * scale;
            }
        }

        void ApplyPose()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var focus = FocusPoint + _pan;
            transform.SetPositionAndRotation(focus + rotation * new Vector3(0f, 0f, -_distance), rotation);
        }

        Vector3 FocusPoint => _focus != null ? _focus.position + Vector3.up * 0.6f : Vector3.up * 0.6f;
    }
}
