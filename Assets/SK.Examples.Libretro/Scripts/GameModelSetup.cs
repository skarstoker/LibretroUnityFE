﻿/* MIT License

 * Copyright (c) 2020 Skurdt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE. */

using SK.Libretro.Unity;
using SK.Utilities.Unity;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SK.Examples
{
    [SelectionBase, DisallowMultipleComponent]
    internal abstract class GameModelSetup : MonoBehaviour
    {
        public bool AnalogDirectionsToDigital = false;
        public bool RewindEnabled             = false;

        public Toggle AnalogDirectionsToDigitalToggle;
        public Toggle RewindToggle;

        public string CoreName { get; set; }
        public string GameDirectory { get; set; }
        public string GameName { get; set; }
        public bool Running => _libretro != null && _libretro.Running;
        public bool InputEnabled
        {
            get => _libretro != null && _libretro.InputEnabled;
            set
            {
                if (_libretro != null)
                    _libretro.InputEnabled = value;
            }
        }

        private LibretroBridge _libretro = null;
        private Transform _viewer        = null;

        private void Awake()
        {
            _viewer = Camera.main.transform;

            GameObject analogToDigitalToggleGameObject = GameObject.Find("AnalogToDigitalToggle");
            if (analogToDigitalToggleGameObject != null)
            {
                AnalogDirectionsToDigitalToggle = analogToDigitalToggleGameObject.GetComponent<Toggle>();
                if (AnalogDirectionsToDigitalToggle != null)
                    AnalogDirectionsToDigitalToggle.isOn = AnalogDirectionsToDigital;
            }

            GameObject rewindToggleGameObject = GameObject.Find("RewindToggle");
            if (rewindToggleGameObject != null)
            {
                RewindToggle = rewindToggleGameObject.GetComponent<Toggle>();
                if (RewindToggle != null)
                    RewindToggle.isOn = RewindEnabled;
            }
        }

        private void Start()
        {
            LoadConfig();

            StartGame();

            if (_libretro != null && _libretro.Running)
                OnLateStart();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                StopGame();
                ApplicationUtils.ExitApp();
                return;
            }

            if (_libretro != null && _libretro.Running)
            {
                OnUpdate();
                _libretro.Update();
            }
        }

        private void OnEnable() => Application.focusChanged += OnApplicationFocusChanged;

        private void OnDisable()
        {
            Application.focusChanged -= OnApplicationFocusChanged;
            StopGame();
        }

        public void Pause() => _libretro?.Pause();

        public void Resume() => _libretro?.Resume();

        public bool SaveState(int index, bool saveScreenshot = true) => _libretro != null && _libretro.SaveState(index, saveScreenshot);

        public bool LoadState(int index) => _libretro != null && _libretro.LoadState(index);

        public void Rewind(bool rewind) => _libretro.Rewind(rewind);

        public void UI_SetAnalogToDigitalInput(bool value) => _libretro?.SetAnalogToDigitalInput(value);

        public void UI_SetRewind(bool value) => _libretro?.SetRewindEnabled(value);

        protected virtual void OnLateStart()
        {
        }

        protected virtual void OnUpdate()
        {
        }

        protected void StartGame()
        {
            if (string.IsNullOrEmpty(CoreName))
            {
                Debug.LogError("Core not set");
                return;
            }

            ScreenNode screen = GetComponentInChildren<ScreenNode>();
            if (screen == null)
            {
                Debug.LogWarning($"ScreenNode not found, adding ScreenNode component to the same node this script is attached to ({name})");
                screen = gameObject.AddComponent<ScreenNode>();
            }

            if (screen.GetComponent<Renderer>() == null)
            {
                Debug.LogError("Component of type Renderer not found");
                return;
            }

            LibretroBridge.Settings settings = new LibretroBridge.Settings
            {
                AnalogDirectionsToDigital = AnalogDirectionsToDigital
            };
            _libretro = new LibretroBridge(screen, _viewer, settings);
            if (!_libretro.Start(CoreName, GameDirectory, GameName))
            {
                StopGame();
                return;
            }
        }

        protected void StopGame()
        {
            _libretro?.Stop();
            _libretro = null;
        }

        private void OnApplicationFocusChanged(bool focus)
        {
            if (!focus)
                _libretro?.Pause();
            else
                _libretro?.Resume();
        }

        /***********************************************************************************************************************
         * Config file
         **********************************************************************************************************************/
        [Serializable]
        protected sealed class ConfigFileContent
        {
            public string Core;
            public string Directory;
            public string Name;
            public bool AnalogDirectionsToDigital;
            public ConfigFileContent(GameModelSetup gameModelSetup)
            {
                Core                      = gameModelSetup.CoreName;
                Directory                 = gameModelSetup.GameDirectory;
                Name                      = gameModelSetup.GameName;
                AnalogDirectionsToDigital = gameModelSetup.AnalogDirectionsToDigital;
            }
        }

        [ContextMenu("Load configuration")]
        public void LoadConfig()
        {
            if (!File.Exists(ConfigFilePath))
                return;

            string json = File.ReadAllText(ConfigFilePath);
            if (string.IsNullOrEmpty(json))
                return;

            ConfigFileContent game = LoadJsonConfig(json);
            if (game == null)
                return;

            CoreName                  = game.Core;
            GameDirectory             = game.Directory;
            GameName                  = game.Name;
            AnalogDirectionsToDigital = game.AnalogDirectionsToDigital;

            if (AnalogDirectionsToDigitalToggle != null)
                AnalogDirectionsToDigitalToggle.isOn = AnalogDirectionsToDigital;
        }

        [ContextMenu("Save configuration")]
        public void SaveConfig()
        {
            string json = GetJsonConfig();
            if (!string.IsNullOrEmpty(json))
                File.WriteAllText(ConfigFilePath, json);
        }

        protected abstract string ConfigFilePath { get; }
        protected abstract ConfigFileContent LoadJsonConfig(string json);
        protected abstract string GetJsonConfig();
    }
}
