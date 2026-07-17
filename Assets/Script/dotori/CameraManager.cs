using System;
using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Script.dotori
{
    [RequireComponent(typeof(PlayerInput))]
    public class CameraManager: MonoBehaviour
    {
        public CinemachineVirtualCamera[] cineVirtual; 
        private int _index = 0; 
        public PlayerGameState state;
        
        private void Start()
        {
            if (cineVirtual.Length == 0) 
            {
                Debug.LogWarning("No virtual camera found. CameraManager shutting down."); 
                gameObject.SetActive(false); 
                return;
            }

            UpdateCameraPriorities();
        }

        public IEnumerator StartMonitoring()
        {
            while (state.IsInPlayground)
                yield return new WaitForSeconds(0.5f);
            UpdateCameraPriorities();
        }

        public void Disable()
        {
            StopCoroutine(StartMonitoring());
            foreach (var cineVirtualCamera in cineVirtual)
            {
                cineVirtualCamera.Priority = 0;
            }
        }

        private void OnPrevious(InputValue value) 
        {
            _index--;
            if (_index < 0) _index = cineVirtual.Length - 1; 
            UpdateCameraPriorities();
        }
        
        private void OnNext(InputValue value) 
        {
            _index++; 
            if (_index >= cineVirtual.Length) _index = 0; 
            UpdateCameraPriorities();
        }

        private void UpdateCameraPriorities()
        {
            for (var i = 0; i < cineVirtual.Length; i++)
            {
                cineVirtual[i].Priority = (i == _index) ? 15 : 0;
            }
        }
    }
}