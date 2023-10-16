// <copyright file="SplashActivity.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.AppCompat.App;

namespace Apollo
{
    /// <summary>
    /// Activity to display the app logo during startup.
    /// </summary>
    [Activity(Theme = "@style/ApolloTheme.Splash", MainLauncher = true, NoHistory = true)]
    public class SplashActivity : AppCompatActivity
    {
        /// <summary>
        /// Triggered when the back button is pressed.
        /// </summary>
        public override void OnBackPressed()
        {
            // Do nothing if back button is pressed to prevent the cancelling of startup
        }

        /// <summary>
        /// Starts the app's <see cref="MainActivity"/>.
        /// </summary>
        protected override void OnResume()
        {
            StartActivity(new Intent(Application.Context, typeof(MainActivity)));

            // Show custom splash screen for Android versions < 31 (Android 12)
            if (Build.VERSION.SdkInt < BuildVersionCodes.S) OverridePendingTransition(Resource.Animation.splash_fade_in, Resource.Animation.splash_fade_out);

            base.OnResume();
        }
    }
}