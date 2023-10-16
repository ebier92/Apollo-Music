// <copyright file="MainActivityPresenter.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using Android.App;
using Android.Content;
using Android.Graphics.Drawables;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using static Com.Sothree.Slidinguppanel.SlidingUpPanelLayout;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace Apollo
{
    /// <summary>
    /// Presenter class for the <see cref="MainActivity"/>.
    /// </summary>
    internal class MainActivityPresenter
    {
        private readonly IMainActivity view;
        private readonly MusicBrowser musicBrowser;
        private HomeFragment homeFragment;
        private PlayerFragment playerFragment;
        private RecommendedFragment recommendedFragment;
        private SearchFragment searchFragment;
        private PlaylistsFragment playlistsFragment;
        private CurrentQueueFragment currentQueueFragment;
        private bool searchFragmentShowing;
        private bool seekInProgress;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainActivityPresenter"/> class.
        /// </summary>
        /// <param name="view">An instance of the <see cref="MainActivity"/>.</param>
        /// <param name="musicBrowser">A connected instance of a <see cref="MusicBrowser"/>.</param>
        public MainActivityPresenter(MainActivity view, MusicBrowser musicBrowser)
        {
            this.view = view;
            this.musicBrowser = musicBrowser;
        }

        /// <summary>
        /// Initializes the home and player fragments when the app starts.
        /// </summary>
        public void InitializeMainFragments()
        {
            homeFragment = new HomeFragment();
            playerFragment = new PlayerFragment();
            view.InflateFragment(playerFragment, Resource.Id.frame_player);
            view.InflateFragment(homeFragment, Resource.Id.content_container);

            // Attach home fragment for network change notifications
            view.Attach(homeFragment);
        }

        /// <summary>
        /// Initializes UI components depending on whether the activity is on its initial startup or coming back from the background.
        /// </summary>
        /// <param name="activityInitialized">Bool to indicate whether the activity has been initialized for the first time.</param>
        /// <param name="panelState">The current <see cref="PanelState"/>.</param>
        public void InitializeOnStart(bool activityInitialized, PanelState panelState)
        {
            // Activity not yet initialized
            if (!activityInitialized)
            {
                view.InitializeUserInterface();
                view.UpdatePanelState(PanelState.Hidden);
                UpdatePanelLayoutAnimation(0);
            } // Update the player based on current state if the activity is initialized
            else
            {
                UpdatePlayerState(musicBrowser.PlaybackState, panelState);
                UpdateSeekbarStates(musicBrowser.PlaybackState);
                UpdatePlayPauseButtonState(musicBrowser.PlaybackState);
            }
        }

        /// <summary>
        /// Extracts possible search queries from incoming intents.
        /// </summary>
        /// <param name="intent">An intent sent to the <see cref="MainActivity"/>.</param>
        public void HandleIntent(Intent intent)
        {
            // Check if the intent contained a search action
            if (intent.Action == Intent.ActionSearch)
            {
                // Extract the query
                var query = intent.GetStringExtra(SearchManager.Query);

                // Save the query to the search history
                SearchRecentSuggestions searchRecentSuggestions = new SearchRecentSuggestions(
                    (MainActivity)view,
                    SearchSuggestionsProvider.Authority,
                    SearchSuggestionsProvider.Mode);
                searchRecentSuggestions.SaveRecentQuery(query, null);

                // Send the query to the search fragment if it is initialized
                if (searchFragment != null)
                    searchFragment.OnQuery(query);
            }
        }

        /// <summary>
        /// Processes requests to export and import app data.
        /// </summary>
        /// <param name="requestCode">A request code indicating the caller of the activity with the result.</param>
        /// <param name="resultCode">A result code indicating the outcome of the process.</param>
        /// <param name="data">The result data.</param>
        public void HandleActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            Android.Net.Uri uri = data?.Data;

            if (resultCode == Result.Ok && uri != null && requestCode == SettingsManager.ExportAppDataFileRequestCode)
                SettingsManager.ExportAppData((MainActivity)view, uri);
            else if (resultCode == Result.Ok && uri != null && requestCode == SettingsManager.ImportAppDataFileRequestCode)
                SettingsManager.ImportAppData((MainActivity)view, uri);
        }

        /// <summary>
        /// Expand a collapsed player panel if clicked.
        /// </summary>
        /// <param name="panelState">The current <see cref="PanelState"/>.</param>
        public void HandlePanelClick(PanelState panelState)
        {
            if (panelState == PanelState.Collapsed)
                view.UpdatePanelState(PanelState.Expanded);
        }

        /// <summary>
        /// Inflates a fragment if it has not yet been initialized and shows it, while hiding any other initialized fragments.
        /// </summary>
        /// <param name="itemId">The bottom navigation item ID corresponding to a requested fragment.</param>
        /// <param name="panelState">The current <see cref="PanelState"/>.</param>
        public void RequestFragment(int itemId, PanelState panelState)
        {
            // Determine the what fragment is being requested based on the navigation item ID
            Fragment requestedFragment;
            switch (itemId)
            {
                case Resource.Id.navigation_home:

                    // Initialize the fragment if it is null
                    if (homeFragment == null)
                    {
                        homeFragment = new HomeFragment();
                        view.InflateFragment(homeFragment, Resource.Id.content_container);

                        // Attach for network change notifications
                        view.Attach(homeFragment);
                    }

                    // Save the fragment reference
                    requestedFragment = homeFragment;

                    break;

                case Resource.Id.navigation_recommended:

                    // Initialize the fragment if it is null
                    if (recommendedFragment == null)
                    {
                        recommendedFragment = new RecommendedFragment();
                        view.InflateFragment(recommendedFragment, Resource.Id.content_container);

                        // Attach for network change notifications
                        view.Attach(recommendedFragment);
                    }

                    // Save the fragment reference
                    requestedFragment = recommendedFragment;

                    break;

                case Resource.Id.navigation_search:

                    // Initialize the fragment if it is null
                    if (searchFragment == null)
                    {
                        searchFragment = new SearchFragment();
                        view.InflateFragment(searchFragment, Resource.Id.content_container);

                        // Attach for network change notification
                        view.Attach(searchFragment);
                    }

                    // Save the fragment reference
                    requestedFragment = searchFragment;

                    break;

                case Resource.Id.navigation_playlists:

                    // Initialize the fragment if it is null
                    if (playlistsFragment == null)
                    {
                        playlistsFragment = new PlaylistsFragment();
                        view.InflateFragment(playlistsFragment, Resource.Id.content_container);
                    }

                    // Save the fragment reference
                    requestedFragment = playlistsFragment;

                    break;

                case Resource.Id.navigation_current_queue:

                    // Initialize the fragment if it is null
                    if (currentQueueFragment == null)
                    {
                        currentQueueFragment = new CurrentQueueFragment();
                        view.InflateFragment(currentQueueFragment, Resource.Id.content_container);

                        // Attach for playback state change notifications
                        view.Attach(currentQueueFragment);
                    }

                    // Save the fragment reference
                    requestedFragment = currentQueueFragment;

                    break;

                default:
                    requestedFragment = null;
                    break;
            }

            // Create an array of all app fragment instances
            var fragments = new Fragment[] { homeFragment, recommendedFragment, searchFragment, playlistsFragment, currentQueueFragment };

            // Loop through all fragment instances and show the fragment if it is the requested fragment and hide it if it is not the requested fragment and is initialized
            foreach (var fragment in fragments)
            {
                if (requestedFragment == fragment)
                    view.ShowFragment(fragment);
                else if (fragment != null)
                    view.HideFragment(fragment);
            }

            // Place focus in the search bar and clear search text if the search fragment is already showing and the user selects the search fragment again (double tapping)
            if (requestedFragment is SearchFragment && searchFragmentShowing)
            {
                searchFragment.RequestSearchViewFocus();
                searchFragment.UpdateSearchQueryText("");
            }

            // Set variable for visibility state of the search fragment
            if (requestedFragment is SearchFragment)
                searchFragmentShowing = true;
            else
                searchFragmentShowing = false;

            // Minimize the player if it is expanded and a new fragment was requested
            if (panelState == PanelState.Expanded)
                view.UpdatePanelState(PanelState.Collapsed);
        }

        /// <summary>
        /// Pauses the seekbar synchronization timer as the user is seeking.
        /// </summary>
        public void StartSeek()
        {
            seekInProgress = true;
            view.StopSeekbarSyncTimer();
        }

        /// <summary>
        /// Updates a track's current position based on the result of a user's seek operation.
        /// </summary>
        /// <param name="position">Integer representing the final seek position selected by the user.</param>
        public void CompleteSeek(int position)
        {
            seekInProgress = false;
            var playbackState = musicBrowser.PlaybackState.State;

            // If the current playback state is playing, paused, or buffering, execute the seek operation and restart the seekbar sync timers.
            if (playbackState == PlaybackStateCode.Playing || playbackState == PlaybackStateCode.Paused || playbackState == PlaybackStateCode.Buffering)
            {
                musicBrowser.SeekTo((long)position);

                // Restart syncing if a track is currently playing
                if (playbackState == PlaybackStateCode.Playing)
                    view.StartSeekbarSyncTimer();
            }
        }

        /// <summary>
        /// Skips to the previous track when the user has clicked the skip to previous button.
        /// </summary>
        public void SkipPrevious()
        {
            musicBrowser.SkipToPrevious();
        }

        /// <summary>
        /// Stops the current track when the user has clicked the stop button.
        /// </summary>
        public void Stop()
        {
            musicBrowser.Stop();
        }

        /// <summary>
        /// Plays or pauses the current track when the user has clicked the play/pause button.
        /// </summary>
        public void PlayOrPause()
        {
            if (musicBrowser.IsConnected)
            {
                var playbackState = musicBrowser.PlaybackState.State;

                if (playbackState == PlaybackStateCode.Playing || playbackState == PlaybackStateCode.Buffering)
                    musicBrowser.Pause();
                else if (playbackState == PlaybackStateCode.Paused)
                    musicBrowser.Play();
                else if (playbackState == PlaybackStateCode.Stopped)
                    musicBrowser.Play();
            }
        }

        /// <summary>
        /// Skips to the next track when the user has clicked the skip to next button.
        /// </summary>
        public void SkipNext()
        {
            musicBrowser.SkipToNext();
        }

        /// <summary>
        /// Updates the panel layout expansion/contraction animation based on the current panel slide offset (0 - 1).
        /// </summary>
        /// <param name="slideOffset">The current slider offset, expressed as a float from 0 (collapsed) to 1 (expanded).</param>
        public void UpdatePanelLayoutAnimation(float slideOffset)
        {
            // Exit if the offset is less than 0
            if (slideOffset < 0)
                return;

            var windowManager = (view as MainActivity).WindowManager;

            int widthPixels, heightPixels;

            // Use current window metrics for API 30+
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                widthPixels = windowManager.CurrentWindowMetrics.Bounds.Width();
                heightPixels = windowManager.CurrentWindowMetrics.Bounds.Height();
            }
            else
            {
                #pragma warning disable CS0618 // Type or member is obsolete
                widthPixels = windowManager.DefaultDisplay.Width;
                heightPixels = windowManager.DefaultDisplay.Height;
                #pragma warning restore CS0618 // Type or member is obsolete
            }

            var displayMetrics = Application.Context.Resources.DisplayMetrics;

            // Calculate min and max dimensions for the album art image transformation
            var minWidth = 99 * displayMetrics.Density;
            var minHeight = 68 * displayMetrics.Density;
            var maxWidth = widthPixels;

            // Set max height for the album art based on screen orientation and pixel density
            int maxHeight;

            if (heightPixels >= widthPixels)
            {
                int maxHeightDivisor;

                if (heightPixels / displayMetrics.Density >= 700)
                    maxHeightDivisor = 2;
                else if (heightPixels / displayMetrics.Density >= 500)
                    maxHeightDivisor = 3;
                else
                    maxHeightDivisor = 5;

                maxHeight = heightPixels / maxHeightDivisor;
            }
            else
            {
                int maxHeightDivisor;

                if (heightPixels / displayMetrics.Density >= 700)
                    maxHeightDivisor = 3;
                else if (heightPixels / displayMetrics.Density >= 350)
                    maxHeightDivisor = 5;
                else
                    maxHeightDivisor = 10;

                maxHeight = heightPixels / maxHeightDivisor;
            }

            // Calculate image width and height
            int imageWidth, imageHeight;

            // Set the width to expand to screen width by the time the offset is at or greater than 25%
            if (slideOffset <= 0.25)
                imageWidth = (int)(minWidth + ((maxWidth - minWidth) * slideOffset * 4));
            else
                imageWidth = (int)maxWidth;

            // Set the height
            imageHeight = (int)(minHeight + ((maxHeight - minHeight) * slideOffset));

            // Calculate opacity and view states for the mini player components and main player components
            float miniPlayerOpacity, mainPlayerOpacity;
            ViewStates miniPlayerViewState, mainPlayerViewState;

            // Set the mini player opacity to fade out by the time the offset is at or greater than 5%
            if (slideOffset <= 0.05)
            {
                miniPlayerOpacity = 1 - (slideOffset * 20);
                miniPlayerViewState = ViewStates.Visible;
            }
            else
            {
                miniPlayerOpacity = 0;
                miniPlayerViewState = ViewStates.Gone;
            }

            // Set the main player to start fading in any time the offset is at or greater than 5%
            if (slideOffset >= 0.5)
            {
                mainPlayerOpacity = (float)((slideOffset - 0.5) * 2);
                mainPlayerViewState = ViewStates.Visible;
            }
            else
            {
                mainPlayerOpacity = 0;
                mainPlayerViewState = ViewStates.Gone;
            }

            // Perform view updates
            view.UpdateAlbumArtDimensions(imageWidth, imageHeight);
            view.UpdatePlayerComponentViewStates(miniPlayerViewState, mainPlayerViewState);
            view.UpdatePlayerComponentOpacities(miniPlayerOpacity, mainPlayerOpacity);
        }

        /// <summary>
        /// Reanimates the player after rotation if the player is expanded.
        /// </summary>
        /// <param name="panelState">The current <see cref="PanelState"/>.</param>
        public void UpdateExpandedPanelAfterRotation(PanelState panelState)
        {
            if (panelState == PanelState.Expanded)
                UpdatePanelLayoutAnimation(1);
        }

        /// <summary>
        /// Updates the player's track info labels and panel state based on a change in playback state.
        /// </summary>
        /// <param name="playbackState">The new <see cref="PlaybackState"/>.</param>
        /// <param name="panelState">The current <see cref="PanelState"/>.</param>
        public void UpdatePlayerState(PlaybackState playbackState, PanelState panelState)
        {
            if (playbackState.State == PlaybackStateCode.Buffering || playbackState.State == PlaybackStateCode.Playing || playbackState.State == PlaybackStateCode.Paused)
            {
                // Get metadata
                var metadata = musicBrowser.Metadata;

                if (metadata != null)
                {
                    var trackName = metadata.GetString(MediaMetadata.MetadataKeyTitle);
                    var artist = metadata.GetString(MediaMetadata.MetadataKeyArtist);
                    var albumArt = metadata.GetBitmap(MediaMetadata.MetadataKeyAlbumArt);
                    var gradientData = metadata.GetString("GRADIENT_DATA").Split(",");
                    var bottomColor = int.Parse(gradientData[0]);
                    var topColor = int.Parse(gradientData[1]);
                    var orientationData = gradientData[2];

                    // Update player and buffering icon
                    view.UpdatePlayerInfo(trackName, artist, albumArt);

                    // Update full screen player background colors with a random gradient orientation
                    GradientDrawable.Orientation orientation;

                    if (orientationData == "TrBl")
                        orientation = GradientDrawable.Orientation.TrBl;
                    else if (orientationData == "TlBr")
                        orientation = GradientDrawable.Orientation.TlBr;
                    else
                        orientation = GradientDrawable.Orientation.TlBr;

                    view.UpdatePlayerBackgroundColors(bottomColor, topColor, orientation);

                    if (playbackState.State == PlaybackStateCode.Buffering)
                        view.UpdatePlayerBufferingIconVisibility(ViewStates.Visible);
                    else
                        view.UpdatePlayerBufferingIconVisibility(ViewStates.Gone);
                }

                // Set panel state to collapsed if it is not already expanded
                if (panelState != PanelState.Expanded)
                    view.UpdatePanelState(PanelState.Collapsed);
            }
            else if (playbackState.State == PlaybackStateCode.Stopped)
            {
                // Update player and background colors
                view.UpdatePlayerInfo(null, null, null);
                view.UpdatePlayerBackgroundColors(view.ThemeManager.ColorBackground.ToArgb(), view.ThemeManager.ColorBackground.ToArgb(), GradientDrawable.Orientation.BrTl);
                view.UpdatePlayerBufferingIconVisibility(ViewStates.Gone);
                view.UpdatePanelState(PanelState.Hidden);
            }
        }

        /// <summary>
        /// Updates the seekbar/progress bar durations and starts/stops the timer to synchronize the progresses as the track plays.
        /// </summary>
        /// <param name="playbackState">The new <see cref="PlaybackState"/>.</param>
        public void UpdateSeekbarStates(PlaybackState playbackState)
        {
            // Update duration when track is buffering
            if (playbackState.State == PlaybackStateCode.Buffering)
            {
                var metadata = musicBrowser.Metadata;
                if (metadata != null)
                {
                    var duration = metadata.GetLong(MediaMetadata.MetadataKeyDuration);

                    // Check if the seekbar duration has changed
                    if (view.SeekbarDuration != duration)
                    {
                        // Update duration
                        view.UpdateSeekbarDurations(duration);

                        // Calculate song minus and seconds duration for the time remaining label
                        var minutesRemaining = (int)System.Math.Floor((decimal)(duration / 60000));
                        var secondsRemaining = (duration % 60000) / 1000;

                        // Create the time remaining label text and set the labels
                        var timeRemainingText = $"{minutesRemaining}:{secondsRemaining.ToString().PadLeft(2, '0')}";
                        view.UpdatePlayerTimeDurations("0:00", timeRemainingText);
                    }
                }
            } // Start sync timer when playing if user is not seeking
            else if (playbackState.State == PlaybackStateCode.Playing && !seekInProgress)
            {
                view.StartSeekbarSyncTimer();
            } // Stop sync timer when paused
            else if (playbackState.State == PlaybackStateCode.Paused)
            {
                view.StopSeekbarSyncTimer();
            } // Stop sync timer and set progresses to 0 when stopped or changing tracks
            else if (playbackState.State == PlaybackStateCode.Stopped || playbackState.State == PlaybackStateCode.SkippingToNext || playbackState.State == PlaybackStateCode.SkippingToPrevious || playbackState.State == PlaybackStateCode.SkippingToQueueItem)
            {
                view.StopSeekbarSyncTimer();
                view.UpdateSeekbarProgresses(0);
            }
        }

        /// <summary>
        /// Updates the track time duration labels on the player based on the synchonization timer.
        /// </summary>
        /// <param name="seekBar">The <see cref="Seekbar"/> driving the label timers.</param>
        public void UpdatePlayerTimerLabels(SeekBar seekBar)
        {
            // Current current track progress and duration from seekbar
            var progress = seekBar.Progress;
            var duration = seekBar.Max;

            // Calculate minutes/seconds passed and minutes/seconds remaining
            var minutesPassed = (int)System.Math.Floor((decimal)progress / 60000);
            var secondsPassed = (progress % 60000) / 1000;
            var minutesRemaining = (int)System.Math.Floor((decimal)((duration - progress) / 60000));
            var secondsRemaining = ((duration - progress) % 60000) / 1000;

            // Create text for timers
            var timePassedText = $"{minutesPassed}:{secondsPassed.ToString().PadLeft(2, '0')}";
            var timeRemainingText = $"{minutesRemaining}:{secondsRemaining.ToString().PadLeft(2, '0')}";

            // Update timer texts
            view.UpdatePlayerTimeDurations(timePassedText, timeRemainingText);
        }

        /// <summary>
        /// Updates the track seekbar progresses based on the synchronization timer.
        /// </summary>
        public void UpdateSeekbarProgresses()
        {
            var position = (int)musicBrowser.PlaybackState.Position;
            view.UpdateSeekbarProgresses(position);
        }

        /// <summary>
        /// Updates the toggled state of the play/pause button based on the current playback state.
        /// </summary>
        /// <param name="playbackState">The current <see cref="PlaybackState"/>.</param>
        public void UpdatePlayPauseButtonState(PlaybackState playbackState)
        {
            if (playbackState.State == PlaybackStateCode.Playing || playbackState.State == PlaybackStateCode.Buffering)
                view.UpdatePlayPauseButtonToPause();
            else
                view.UpdatePlayPauseButtonToPlay();
        }
    }
}