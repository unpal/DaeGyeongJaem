using System;
using System.Collections;
using Cinemachine;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Script.dotori
{
    [RequireComponent(typeof(PlayerInput))]
    public class CameraManager: MonoBehaviour
    {

        public static CameraManager Instance;

        public CinemachineVirtualCamera[] cineVirtual; 
        private int _index = 0; 
        public PlayerGameState state;
        private bool _enabled = false;
        private bool _wasInPlayground = true;
        
        private void Start()
        {
            Instance = this;
            if (cineVirtual.Length == 0) 
            {
                Debug.LogWarning("No virtual camera found. CameraManager shutting down."); 
                gameObject.SetActive(false); 
                return;
            }

            UpdateCameraPriorities();
        }

        private void Update()
        {
            if (!state) return;

            bool inPlayground = state.IsInPlayground;
            if (_wasInPlayground && !inPlayground)
            {
                // 관전 상태로 전환
                Debug.Log("Monitoring End - Entering Spectator Mode");
                _enabled = true;
                UpdateCameraPriorities();
            }
            else if (!_wasInPlayground && inPlayground)
            {
                // 플레이 상태로 전환 (라운드 시작)
                Debug.Log("Monitoring Start - Entering Playground");
                _enabled = false;
                SetZero();
            }

            _wasInPlayground = inPlayground;
        }

        private void OnPrevious(InputValue value)
        {
            if (!_enabled) return;
            _index--;
            if (_index < 0) _index = cineVirtual.Length - 1; 
            UpdateCameraPriorities();
        }
        
        private void OnNext(InputValue value) 
        {
            if (!_enabled) return;
            _index++; 
            if (_index >= cineVirtual.Length) _index = 0; 
            UpdateCameraPriorities();
        }

        private void SetZero()
        {
            Debug.Log("SetZero");
            foreach (var cineVirtualCamera in cineVirtual)
            {
                cineVirtualCamera.Priority = 0;
            }
        }
        private void UpdateCameraPriorities()
        {
            Debug.Log("UpdateCameraPriorities");
            if (!_enabled) return;
            for (var i = 0; i < cineVirtual.Length; i++)
            {
                cineVirtual[i].Priority = (i == _index) ? 150 : 0;
            }
        }
    }
}