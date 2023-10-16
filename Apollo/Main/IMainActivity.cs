// <copyright file="IMainActivity.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Media.Session;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using Com.Sothree.Slidinguppanel;

namespace Apollo
{
    internal interface IMainActivity
    {
        bool AppVisible { get; }

        MusicBrowser MusicBrowser { get; }

        long SeekbarDuration { get; }

        ThemeManager ThemeManager { get; }

        void Attach(MainActivity.INetworkLostObserver networkLostObserver);

        void Attach(MainActivity.IPlaybackStateChangedObserver playbackStateChangedObserver);

        void HideFragment(Fragment fragment);

        void InflateFragment(Fragment fragment, int resourceId);

        void InitializeUserInterface();

        void NotifyNetworkLost();

        void NotifyPlaybackStateChange(PlaybackState playbackState);

        void OnBackPressed();

        void OnConfigurationChanged(Configuration newConfig);

        bool OnNavigationItemSelected(IMenuItem item);

        void OnPanelSlide(View view, float slideOffset);

        void OnPanelStateChanged(View p0, SlidingUpPanelLayout.PanelState p1, SlidingUpPanelLayout.PanelState p2);

        void OnPlaybackStateChanged(PlaybackState playbackState);

        void OnProgressChanged(SeekBar seekBar, int progress, bool fromUser);

        void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults);

        void OnStartTrackingTouch(SeekBar seekBar);

        void OnStopTrackingTouch(SeekBar seekBar);

        void OpenCurrentQueueFragment();

        void RestartApp();

        void ShowFragment(Fragment fragment);

        void StartSeekbarSyncTimer();

        void StopSeekbarSyncTimer();

        void UpdateAlbumArtDimensions(int width, int height);

        void UpdatePanelState(SlidingUpPanelLayout.PanelState panelState);

        void UpdatePlayerBackgroundColors(int bottomColor, int topColor, GradientDrawable.Orientation orientation);

        void UpdatePlayerBufferingIconVisibility(ViewStates bufferViewState);

        void UpdatePlayerComponentOpacities(float miniPlayerOpacity, float mainPlayerOpacity);

        void UpdatePlayerComponentViewStates(ViewStates miniPlayerViewState, ViewStates mainPlayerViewState);

        void UpdatePlayerInfo(string trackName, string artist, Bitmap albumArt);

        void UpdatePlayerTimeDurations(string timePassed, string timeRemaining);

        void UpdatePlayPauseButtonToPause();

        void UpdatePlayPauseButtonToPlay();

        void UpdateSeekbarDurations(long duration);

        void UpdateSeekbarProgresses(int position);
    }
}