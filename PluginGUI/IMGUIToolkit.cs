﻿using System.Collections.Generic;
using Donuts.Models;
using UnityEngine;

namespace Donuts
{
    public class ImGUIToolkit
    {
        private static Dictionary<int, bool> dropdownStates = new Dictionary<int, bool>();
        private static Dictionary<int, bool> accordionStates = new Dictionary<int, bool>();
        private static GUIStyle dropdownStyle;
        private static GUIStyle dropdownButtonStyle;
        private static GUIStyle toggleStyle;
        private static GUIStyle accordionButtonStyle;
        private static GUIStyle tooltipStyle;
        private static GUIStyle textFieldStyle;

        public static void InitializeStyles()
        {
            dropdownStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 25,
                fontSize = 18
            };

            dropdownButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 25,
                fontSize = 18
            };

            toggleStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            accordionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 30,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };

            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 18
            };

            tooltipStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                wordWrap = true
            };

            // Create textures for the toggle button states
            CreateToggleButtonTextures();
        }

        internal static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private static void CreateToggleButtonTextures()
        {
            toggleStyle.normal.background = MakeTex(1, 1, Color.gray);
            toggleStyle.hover.background = MakeTex(1, 1, Color.gray);
            toggleStyle.active.background = MakeTex(1, 1, Color.gray);

            toggleStyle.onNormal.background = MakeTex(1, 1, Color.red);
            toggleStyle.onHover.background = MakeTex(1, 1, Color.red);
            toggleStyle.onActive.background = MakeTex(1, 1, Color.red);
        }

        internal static int Dropdown<T>(Setting<T> setting, int selectedIndex)
        {
            // Check if the Options list is properly initialized and log error if needed
            if (setting.LogErrorOnceIfOptionsInvalid())
            {
                return selectedIndex; // Return the current index without drawing the button
            }

            // Ensure selectedIndex is within bounds
            if (selectedIndex >= setting.Options.Length)
            {
                selectedIndex = 0;
            }

            int dropdownId = GUIUtility.GetControlID(FocusType.Passive);

            if (!dropdownStates.ContainsKey(dropdownId))
            {
                dropdownStates[dropdownId] = false;
            }

            GUILayout.BeginHorizontal();

            // Draw label with tooltip
            GUIContent labelContent = new GUIContent(setting.Name, setting.ToolTipText);
            GUILayout.Label(labelContent, GUILayout.Width(200)); // Increased width

            // Draw button with tooltip
            GUIContent buttonContent = new GUIContent(setting.Options[selectedIndex]?.ToString(), setting.ToolTipText);
            if (GUILayout.Button(buttonContent, dropdownStyle, GUILayout.Width(300)))
            {
                dropdownStates[dropdownId] = !dropdownStates[dropdownId];
            }

            GUILayout.EndHorizontal();

            if (dropdownStates[dropdownId])
            {
                for (int i = 0; i < setting.Options.Length; i++)
                {
                    GUIContent optionContent = new GUIContent(setting.Options[i]?.ToString(), setting.ToolTipText);
                    if (GUILayout.Button(optionContent, dropdownButtonStyle, GUILayout.Width(300)))
                    {
                        selectedIndex = i;
                        setting.Value = setting.Options[i];
                        dropdownStates[dropdownId] = false;
                    }
                }
            }

            // Use the centralized ShowTooltip method
            ShowTooltip();

            return selectedIndex;
        }
        public static float Slider(string label, string toolTip, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, GUILayout.Width(200));
            GUILayout.Space(10);
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(300));

            string valueStr = value.ToString("F2");
            valueStr = GUILayout.TextField(valueStr, textFieldStyle, GUILayout.Width(100));

            if (float.TryParse(valueStr, out float parsedValue))
            {
                value = Mathf.Clamp(parsedValue, min, max);
            }

            GUILayout.EndHorizontal();

            ShowTooltip();

            return value;
        }

        public static int Slider(string label, string toolTip, int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, GUILayout.Width(200));
            GUILayout.Space(10);
            value = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(300)));

            string valueStr = value.ToString();
            valueStr = GUILayout.TextField(valueStr, textFieldStyle, GUILayout.Width(100));

            if (int.TryParse(valueStr, out int parsedValue))
            {
                value = Mathf.Clamp(parsedValue, min, max);
            }

            GUILayout.EndHorizontal();

            ShowTooltip();

            return value;
        }

        public static string TextField(string label, string toolTip, string text)
        {
            GUILayout.BeginHorizontal();
            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, GUILayout.Width(200));
            GUILayout.Space(10);
            text = GUILayout.TextField(text, textFieldStyle, GUILayout.Width(300));
            GUILayout.EndHorizontal();

            ShowTooltip();

            return text;
        }

        public static bool Toggle(string label, string toolTip, bool value)
        {
            GUILayout.BeginHorizontal();
            GUIContent labelContent = new GUIContent(label, toolTip);
            GUILayout.Label(labelContent, GUILayout.Width(200));
            GUILayout.Space(10);

            // Apply the custom toggle style
            GUIContent toggleContent = new GUIContent(value ? "YES" : "NO", toolTip);
            bool newValue = GUILayout.Toggle(value, toggleContent, toggleStyle, GUILayout.Width(150), GUILayout.Height(35));

            GUILayout.EndHorizontal();

            ShowTooltip();

            return newValue;
        }

        public static bool Button(string label, string toolTip, GUIStyle style = null)
        {
            if (style == null)
            {
                style = GUI.skin.button;
            }

            GUIContent buttonContent = new GUIContent(label, toolTip);
            bool result = GUILayout.Button(buttonContent, style, GUILayout.Width(200));

            ShowTooltip();

            return result;
        }

        public static void Accordion(string label, string toolTip, System.Action drawContents)
        {
            int accordionId = GUIUtility.GetControlID(FocusType.Passive);

            if (!accordionStates.ContainsKey(accordionId))
            {
                accordionStates[accordionId] = false;
            }

            GUIContent buttonContent = new GUIContent(label, toolTip);
            if (GUILayout.Button(buttonContent, accordionButtonStyle))
            {
                accordionStates[accordionId] = !accordionStates[accordionId];
            }

            if (accordionStates[accordionId])
            {
                GUILayout.BeginVertical(GUI.skin.box);
                drawContents();
                GUILayout.EndVertical();
            }

            ShowTooltip();
        }

        private static void ShowTooltip()
        {
            if (!string.IsNullOrEmpty(GUI.tooltip))
            {
                Vector2 mousePosition = Event.current.mousePosition;
                Vector2 size = tooltipStyle.CalcSize(new GUIContent(GUI.tooltip));
                size.y = tooltipStyle.CalcHeight(new GUIContent(GUI.tooltip), size.x);
                Rect tooltipRect = new Rect(mousePosition.x, mousePosition.y - size.y, size.x, size.y);
                GUI.Box(tooltipRect, GUI.tooltip, tooltipStyle);
            }
        }

        //used for finding the correct selection on dropdowns
        internal static int FindIndex<T>(Setting<T> setting)
        {
            for (int i = 0; i < setting.Options.Length; i++)
            {
                if (EqualityComparer<T>.Default.Equals(setting.Options[i], setting.Value))
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
