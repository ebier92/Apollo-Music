// <copyright file="ThemeManager.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Graphics;
using AndroidX.Core.Content;

namespace Apollo
{
    /// <summary>
    /// Class to manage and expose theme attributes for use throughout the app.
    /// </summary>
    internal class ThemeManager
    {
        private Dictionary<string, int> attributes = new Dictionary<string, int>()
        {
            { "ColorPrimary", Resource.Color.blue200 },
            { "ColorPrimaryDark", Resource.Color.blue300 },
            { "ColorAccent", Resource.Color.orange200 },
            { "ColorForeground", Resource.Color.grey300 },
            { "ColorBackground", Resource.Color.grey400 },
            { "WindowBackground", Resource.Color.grey400 },
            { "StatusBarColor", Resource.Color.blue200 },
            { "ColorControlNormal", Resource.Color.blue100 },
            { "ColorControlActivated", Resource.Color.orange100 },
            { "ColorButtonNormal", Resource.Color.grey200 },
            { "TextColorPrimary", Resource.Color.white },
            { "TextColorSecondary", Resource.Color.grey200 },
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeManager"/> class.
        /// </summary>
        /// <param name="context">The application <see cref="Context"/>.</param>
        public ThemeManager(Context context)
        {
            foreach (var attribute in attributes)
            {
                switch (attribute.Key.ToString())
                {
                    case "ColorPrimary":
                        ColorPrimary = new Color(ContextCompat.GetColor(Application.Context, attribute.Value));
                        break;
                    case "ColorPrimaryDark":
                        ColorPrimaryDark = new Color(ContextCompat.GetColor(Application.Context, attribute.Value));
                        break;
                    case "ColorAccent":
                        ColorAccent = new Color(ContextCompat.GetColor(Application.Context, attribute.Value));
                        break;
                    case "ColorForeground":
                        ColorForeground = new Color(ContextCompat.GetColor(Application.Context, attribute.Value));
                        break;
                    case "ColorBackground":
                        ColorBackground = new Color(ContextCompat.GetColor(Application.Context, attribute.Value));
                        break;
                    case "WindowBackground":
                        WindowBackground = new Color(ContextCompat.GetColor(Application.Context, attribute.Value));
                        break;
                    case "StatusBarColor":
                        StatusBarColor = new Color(ContextCompat.GetColor(Application.Context, attribute.Value));
                        break;
                    case "ColorControlNormal":
                        ColorControlNormal = new Color(ContextCompat.GetColor(Application.Context, attribute.Value));
                        break;
                    case "ColorControlActivated":
                        ColorControlActivated = new Color(ContextCompat.GetColor(Application.Context, attribute.Value));
                        break;
                    case "ColorButtonNormal":
                        ColorControlNormal = new Color(ContextCompat.GetColor(Application.Context, attribute.Value));
                        break;
                    case "TextColorPrimary":
                        TextColorSecondary = new Color(ContextCompat.GetColor(Application.Context, attribute.Value));
                        break;
                }
            }
        }

        public Color ColorPrimary { get; private set; }

        public Color ColorPrimaryDark { get; private set; }

        public Color ColorAccent { get; private set; }

        public Color ColorForeground { get; private set; }

        public Color ColorBackground { get; private set; }

        public Color WindowBackground { get; private set; }

        public Color StatusBarColor { get; private set; }

        public Color ColorControlNormal { get; private set; }

        public Color ColorControlActivated { get; private set; }

        public Color ColorButtonNormal { get; private set; }

        public Color TextColorPrimary { get; private set; }

        public Color TextColorSecondary { get; private set; }
    }
}