using System;
using UnityEngine;

namespace TestMod
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private UIManager _uiManager;

        private void Awake()
        {
            _uiManager = new UIManager();
        }

        private void OnEnable()
        {

        }

        private void OnDisable()
        {

        }

        private void Update()
        {

            if (Input.GetKeyDown(KeyCode.F2))
            {
                _uiManager.IsVisible = !_uiManager.IsVisible;
            }
        }

        public void OnGUI()
        {
            _uiManager.OnGUI();
        }
    }
}
