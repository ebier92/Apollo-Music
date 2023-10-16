// <copyright file="MainActivity.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Timers;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Media.Session;
using Android.Net;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Com.Sothree.Slidinguppanel;
using FFImageLoading;
using Google.Android.Material.BottomNavigation;
using static Com.Sothree.Slidinguppanel.SlidingUpPanelLayout;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace Apollo
{
    /// <summary>
    /// Main Apollo app activity. Contains the fragments that make up the app and allows the user to select fragments from a bottom navigation bar.
    /// </summary>
    [Activity(Label = "@string/app_name", Exported = true, Theme = "@style/ApolloTheme", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout, LaunchMode = LaunchMode.SingleTop)]
    [IntentFilter(new[] { "android.intent.action.SEARCH" })]
    [MetaData("android.app.searchable", Resource = "@xml/searchable")]
    internal class MainActivity : AppCompatActivity, IMainActivity, BottomNavigationView.IOnItemSelectedListener, IPanelSlideListener, SeekBar.IOnSeekBarChangeListener
    {
        private readonly ConnectivityManager connectivityManager;
        private readonly MainActivityPresenter presenter;
        private readonly Timer seekbarSyncTimer;
        private readonly MusicBrowser.MediaControllerCallback mediaControllerCallback;
        private readonly NetworkStatus.NetworkChangeCallback networkChangeCallback;
        private readonly List<IPlaybackStateChangedObserver> playbackStateChangedObservers;
        private readonly List<INetworkLostObserver> networkLostObservers;
        private BottomNavigationView bottomNavigation;
        private SlidingUpPanelLayout panelLayout;
        private LinearLayout playerGradientBackground;
        private LinearLayout playerSolidBackground;
        private ImageView playerAlbumArt;
        private ProgressBar songBufferingProgressBar;
        private TextView miniPlayerSongTitle;
        private ProgressBar miniPlayerProgressBar;
        private Button miniPlayerPlayPauseButton;
        private Button miniPlayerSkipNextButton;
        private TextView playerSongTitle;
        private TextView playerArtistName;
        private TextView playerTimePassed;
        private TextView playerTimeRemaining;
        private SeekBar playerSeekBar;
        private Button playerSkipPreviousButton;
        private Button playerStopButton;
        private Button playerPlayPauseButton;
        private Button playerSkipNextButton;
        private bool activityInitialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainActivity"/> class.
        /// </summary>
        public MainActivity()
        {
            // Intialize main objects
            connectivityManager = (ConnectivityManager)Application.Context.GetSystemService(ConnectivityService);
            MusicBrowser = new MusicBrowser();
            mediaControllerCallback = new MusicBrowser.MediaControllerCallback();
            networkChangeCallback = new NetworkStatus.NetworkChangeCallback();
            presenter = new MainActivityPresenter(this, MusicBrowser);
            seekbarSyncTimer = new Timer();

            // Initialize observer lists
            playbackStateChangedObservers = new List<IPlaybackStateChangedObserver>();
            networkLostObservers = new List<INetworkLostObserver>();

            // Notify observers of playback state change
            mediaControllerCallback.OnPlaybackStateChangedAction = playbackState =>
            {
                // Call local playback state changed event
                OnPlaybackStateChanged(playbackState);

                // Notify each observer of a playback state change
                NotifyPlaybackStateChange(playbackState);
            };

            networkChangeCallback.OnAvailableAction = network =>
            {
                // Implemented to satify callback
            };

            // Notify observers that connectivity has been lost
            networkChangeCallback.OnLostAction = network =>
            {
                NotifyNetworkLost();
            };

            // Implemented to satisfy callback
            mediaControllerCallback.OnQueueChangedAction = queue => { };
        }

        /// <summary>
        /// Interface to notify observer fragments of a playback state change.
        /// </summary>
        public interface IPlaybackStateChangedObserver
        {
            void NotifyPlaybackStateChange(PlaybackState playbackState);
        }

        /// <summary>
        /// Interface to notify observer fragments that network connectivity has been lost.
        /// </summary>
        public interface INetworkLostObserver
        {
            void NotifyNetworkLost();
        }

        /// <summary>
        /// Gets the <see cref="MusicBrowser"/> to be used by the app to interact with and control track media.
        /// </summary>
        public MusicBrowser MusicBrowser { get; private set; }

        /// <summary>
        /// Gets the app <see cref="ThemeManager"/>.
        /// </summary>
        public ThemeManager ThemeManager { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the app is currently visible in the foreground to the user.
        /// </summary>
        public bool AppVisible { get; private set; }

        /// <summary>
        /// Gets the maximum duration of the player seek bar.
        /// </summary>
        public long SeekbarDuration
        {
            get
            {
                return playerSeekBar.Max;
            }
        }

        /// <summary>
        /// Listens for the selection of a menu item on the app's bottom navigation bar and begins inflating the corresponding fragment.
        /// </summary>
        /// <param name="item">Selected menu item.</param>
        /// <returns>Bool value of True required by <see cref="BottomNavigationView.IOnNavigationItemSelectedListener"/>.</returns>
        public bool OnNavigationItemSelected(IMenuItem item)
        {
            presenter.RequestFragment(item.ItemId, panelLayout.GetPanelState());

            return true;
        }

        /// <summary>
        /// Event triggered when player panel background is clicked. Expands the panel if collapsed.
        /// </summary>
        public void OnPanelClicked()
        {
            presenter.HandlePanelClick(panelLayout.GetPanelState());
        }

        /// <summary>
        /// Event triggered when sliding up panel is being moved. Updates the movement animations for all panel elements.
        /// </summary>
        /// <param name="view">The parent <see cref="View"/>.</param>
        /// <param name="slideOffset">The current slider offset, expressed as a float from 0 (collapsed) to 1 (expanded).</param>
        public void OnPanelSlide(View view, float slideOffset)
        {
            presenter.UpdatePanelLayoutAnimation(slideOffset);
        }

        /// <summary>
        /// Event triggered when the sliding up panel changes state.
        /// </summary>
        /// <param name="p0">The parent <see cref="View"/>.</param>
        /// <param name="p1">The previous <see cref="PanelState"/>.</param>
        /// <param name="p2">The current <see cref="PanelState"/>.</param>
        public void OnPanelStateChanged(View p0, PanelState p1, PanelState p2)
        {
            // Implemented to satisfy interface
        }

        /// <summary>
        /// Event triggered when the playback state of the music service is changed.
        /// </summary>
        /// <param name="playbackState">The new current <see cref="PlaybackState"/>.</param>
        public void OnPlaybackStateChanged(PlaybackState playbackState)
        {
            presenter.UpdatePlayerState(playbackState, panelLayout.GetPanelState());
            presenter.UpdateSeekbarStates(playbackState);
            presenter.UpdatePlayPauseButtonState(playbackState);
        }

        /// <summary>
        /// Event triggered when the user begins a seeking operation on a <see cref="SeekBar"/>.
        /// </summary>
        /// <param name="seekBar">The <see cref="SeekBar"/> that is being used for the seeking operation.</param>
        public void OnStartTrackingTouch(SeekBar seekBar)
        {
            presenter.StartSeek();
        }

        /// <summary>
        /// Event triggered when the user ends a seeking operation on a <see cref="SeekBar"/>.
        /// </summary>
        /// <param name="seekBar">The <see cref="SeekBar"/> that has completed a seek operation.</param>
        public void OnStopTrackingTouch(SeekBar seekBar)
        {
            presenter.CompleteSeek(seekBar.Progress);
        }

        /// <summary>
        /// Event triggered when the progress of a seekbar is updated.
        /// </summary>
        /// <param name="seekBar">The <see cref="SeekBar"/> whose progress has changed.</param>
        /// <param name="progress">An integer representing the current track progress.</param>
        /// <param name="fromUser">Bool representing whether the progress change was triggered by the user.</param>
        public void OnProgressChanged(SeekBar seekBar, int progress, bool fromUser)
        {
            presenter.UpdatePlayerTimerLabels(seekBar);
        }

        /// <summary>
        /// Initializes the UI components when the app starts.
        /// </summary>
        public void InitializeUserInterface()
        {
            // Initialize UI components
            playerAlbumArt = FindViewById<ImageView>(Resource.Id.img_player_album_art);
            playerGradientBackground = FindViewById<LinearLayout>(Resource.Id.player_gradient_background);
            playerSolidBackground = FindViewById<LinearLayout>(Resource.Id.player_solid_background);
            songBufferingProgressBar = FindViewById<ProgressBar>(Resource.Id.progress_bar_song_loading);
            miniPlayerSongTitle = FindViewById<TextView>(Resource.Id.txt_mini_player_song_title);
            miniPlayerPlayPauseButton = FindViewById<Button>(Resource.Id.btn_mini_player_play_pause);
            miniPlayerSkipNextButton = FindViewById<Button>(Resource.Id.btn_mini_player_skip_next);
            miniPlayerProgressBar = FindViewById<ProgressBar>(Resource.Id.progress_bar_mini_player);
            playerSongTitle = FindViewById<TextView>(Resource.Id.txt_song_title);
            playerArtistName = FindViewById<TextView>(Resource.Id.txt_artist_name);
            playerTimePassed = FindViewById<TextView>(Resource.Id.txt_time_passed);
            playerTimeRemaining = FindViewById<TextView>(Resource.Id.txt_time_remaining);
            playerSkipNextButton = FindViewById<Button>(Resource.Id.btn_skip_next);
            playerStopButton = FindViewById<Button>(Resource.Id.btn_stop);
            playerPlayPauseButton = FindViewById<Button>(Resource.Id.btn_play_pause);
            playerSkipPreviousButton = FindViewById<Button>(Resource.Id.btn_skip_previous);

            // Initialize main player seekbar
            playerSeekBar = FindViewById<SeekBar>(Resource.Id.seek_bar);
            playerSeekBar.SetOnSeekBarChangeListener(this);

            // Set up player button controls
            miniPlayerPlayPauseButton.Click += (sender, e) =>
            {
                presenter.PlayOrPause();
            };

            miniPlayerSkipNextButton.Click += (sender, e) =>
            {
                presenter.SkipNext();
            };

            playerSkipPreviousButton.Click += (sender, e) =>
            {
                presenter.SkipPrevious();
            };

            playerStopButton.Click += (sender, e) =>
            {
                presenter.Stop();
            };

            playerPlayPauseButton.Click += (sender, e) =>
            {
                presenter.PlayOrPause();
            };

            playerSkipNextButton.Click += (sender, e) =>
            {
                presenter.SkipNext();
            };

            activityInitialized = true;
        }

        /// <summary>
        /// Updates the size of the album art based on the sliding panel offset as part of the sliding panel animation.
        /// </summary>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        public void UpdateAlbumArtDimensions(int width, int height)
        {
            var layoutParameters = playerAlbumArt.LayoutParameters;
            layoutParameters.Width = width;
            layoutParameters.Height = height;
            playerAlbumArt.LayoutParameters = layoutParameters;
        }

        /// <summary>
        /// Updates the panel to a specific <see cref="PanelState"/>.
        /// </summary>
        /// <param name="panelState">The <see cref="PanelState"/> to set the panel to.</param>
        public void UpdatePanelState(PanelState panelState)
        {
            panelLayout.SetPanelState(panelState);
        }

        /// <summary>
        /// Updates the visibility state of the buffering icon over the track's album art.
        /// </summary>
        /// <param name="bufferViewState">The view state of the buffering icon.</param>
        public void UpdatePlayerBufferingIconVisibility(ViewStates bufferViewState)
        {
            songBufferingProgressBar.Visibility = bufferViewState;
        }

        /// <summary>
        /// Updates the opacities of sliding panel elements as part of the sliding panel animation.
        /// </summary>
        /// <param name="miniPlayerOpacity">A float representing the opacity of the mini player.</param>
        /// <param name="mainPlayerOpacity">A float representing the opacity of the main player.</param>
        public void UpdatePlayerComponentOpacities(float miniPlayerOpacity, float mainPlayerOpacity)
        {
            // Update opacities for mini player components
            miniPlayerSongTitle.Alpha = miniPlayerOpacity;
            miniPlayerProgressBar.Alpha = miniPlayerOpacity;
            miniPlayerPlayPauseButton.Alpha = miniPlayerOpacity;
            miniPlayerSkipNextButton.Alpha = miniPlayerOpacity;

            // Update opacities for main player components
            playerSongTitle.Alpha = mainPlayerOpacity;
            playerArtistName.Alpha = mainPlayerOpacity;
            playerTimePassed.Alpha = mainPlayerOpacity;
            playerTimeRemaining.Alpha = mainPlayerOpacity;
            playerSeekBar.Alpha = mainPlayerOpacity;
            playerSkipPreviousButton.Alpha = mainPlayerOpacity;
            playerPlayPauseButton.Alpha = mainPlayerOpacity;
            playerSkipNextButton.Alpha = mainPlayerOpacity;
            playerSolidBackground.Alpha = 1 - mainPlayerOpacity;
        }

        /// <summary>
        /// Updates the visibility states of sliding panel elements as part of the sliding panel animation.
        /// </summary>
        /// <param name="miniPlayerViewState">The view state of the mini player.</param>
        /// <param name="mainPlayerViewState">The view state of the main player.</param>
        public void UpdatePlayerComponentViewStates(ViewStates miniPlayerViewState, ViewStates mainPlayerViewState)
        {
            // Update view states for mini player components
            miniPlayerSongTitle.Visibility = miniPlayerViewState;
            miniPlayerProgressBar.Visibility = miniPlayerViewState;
            miniPlayerPlayPauseButton.Visibility = miniPlayerViewState;
            miniPlayerSkipNextButton.Visibility = miniPlayerViewState;

            // Update opacities for main player components
            playerSongTitle.Visibility = mainPlayerViewState;
            playerArtistName.Visibility = mainPlayerViewState;
            playerTimePassed.Visibility = mainPlayerViewState;
            playerTimeRemaining.Visibility = mainPlayerViewState;
            playerSeekBar.Visibility = mainPlayerViewState;
            playerSkipPreviousButton.Visibility = mainPlayerViewState;
            playerPlayPauseButton.Visibility = mainPlayerViewState;
            playerSkipNextButton.Visibility = mainPlayerViewState;
        }

        /// <summary>
        /// Sets the gradient colors for the background of the full screen player panel.
        /// </summary>
        /// <param name="bottomColor">The RGB color at the bottom of the gradient.</param>
        /// <param name="topColor">The RGB color at the top of the gradient.</param>
        /// <param name="orientation">The gradient orientation.</param>
        public void UpdatePlayerBackgroundColors(int bottomColor, int topColor, GradientDrawable.Orientation orientation)
        {
            var gradientDrawable = new GradientDrawable(
                orientation,
                new int[] { ThemeManager.ColorBackground.ToArgb(), topColor, bottomColor });
            gradientDrawable.SetGradientCenter(0.5F, 0.9F);
            gradientDrawable.SetGradientType(GradientType.LinearGradient);

            playerGradientBackground.Background = gradientDrawable;
        }

        /// <summary>
        /// Updates the track metadata related labels and album art image.
        /// </summary>
        /// <param name="trackName">The name of the current track.</param>
        /// <param name="artist">The artist of the current track.</param>
        /// <param name="albumArt">A <see cref="Bitmap"/> of the current track's album art.</param>
        public void UpdatePlayerInfo(string trackName, string artist, Bitmap albumArt)
        {
            // Set text and album art
            miniPlayerSongTitle.Text = trackName;
            playerSongTitle.Text = trackName;
            playerArtistName.Text = artist;
            playerAlbumArt.SetImageBitmap(albumArt);
        }

        /// <summary>
        /// Updates the player labels of time passed and time remaining for the current track in minutes and seconds.
        /// </summary>
        /// <param name="timePassed">Text representing the minutes and seconds passed for the track.</param>
        /// <param name="timeRemaining">Text representing the minutes and seconds remaining for the track.</param>
        public void UpdatePlayerTimeDurations(string timePassed, string timeRemaining)
        {
            playerTimePassed.Text = timePassed;
            playerTimeRemaining.Text = timeRemaining;
        }

        /// <summary>
        /// Updates the play pause button to the pause state.
        /// </summary>
        public void UpdatePlayPauseButtonToPause()
        {
            miniPlayerPlayPauseButton.SetBackgroundResource(Resource.Drawable.ic_pause_control);
            playerPlayPauseButton.SetBackgroundResource(Resource.Drawable.ic_pause_control);
        }

        /// <summary>
        /// Updates the play pause button to the play state.
        /// </summary>
        public void UpdatePlayPauseButtonToPlay()
        {
            miniPlayerPlayPauseButton.SetBackgroundResource(Resource.Drawable.ic_play_control);
            playerPlayPauseButton.SetBackgroundResource(Resource.Drawable.ic_play_control);
        }

        /// <summary>
        /// Updates the maximum durations of the seekbars.
        /// </summary>
        /// <param name="duration">Long representing seekbar track duration in milliseconds.</param>
        public void UpdateSeekbarDurations(long duration)
        {
            miniPlayerProgressBar.Max = (int)duration;
            playerSeekBar.Max = (int)duration;
        }

        /// <summary>
        /// Updates the current progress positions of both the mini player and main player seekbars.
        /// </summary>
        /// <param name="position">Integer representing the current desired position of each seekbar.</param>
        public void UpdateSeekbarProgresses(int position)
        {
            miniPlayerProgressBar.Progress = position;
            playerSeekBar.Progress = position;
        }

        /// <summary>
        /// Attaches an <see cref="IPlaybackStateChangedObserver"/> for notifications.
        /// </summary>
        /// <param name="playbackStateChangedObserver">The <see cref="IPlaybackStateChangedObserver"/> to attach.</param>
        public void Attach(IPlaybackStateChangedObserver playbackStateChangedObserver)
        {
            playbackStateChangedObservers.Add(playbackStateChangedObserver);
        }

        /// <summary>
        /// Attaches an <see cref="INetworkLostObserver"/> for notifications.
        /// </summary>
        /// <param name="networkLostObserver">The <see cref="INetworkLostObserver"/> to attach.</param>
        public void Attach(INetworkLostObserver networkLostObserver)
        {
            networkLostObservers.Add(networkLostObserver);
        }

        /// <summary>
        /// Notifies all observers of a <see cref="PlaybackState"/> change.
        /// </summary>
        /// <param name="playbackState">The new <see cref="PlaybackState"/>.</param>
        public void NotifyPlaybackStateChange(PlaybackState playbackState)
        {
            // Notify each observer of a playback state change
            foreach (var playbackStateChangedObserver in playbackStateChangedObservers)
            {
                playbackStateChangedObserver.NotifyPlaybackStateChange(playbackState);
            }
        }

        /// <summary>
        /// Notifies observers of a loss of network connectivity.
        /// </summary>
        public void NotifyNetworkLost()
        {
            // Notify each observer of a loss of connectivity
            foreach (var networkLostObserver in networkLostObservers)
            {
                networkLostObserver.NotifyNetworkLost();
            }
        }

        /// <summary>
        /// Triggers a restart of the app.
        /// </summary>
        public void RestartApp()
        {
            // Create activity restart intent
            var packageManager = PackageManager;
            var intent = packageManager.GetLaunchIntentForPackage(PackageName);
            var componentName = intent.Component;
            var mainIntent = Intent.MakeRestartActivityTask(componentName);

            // Send restart intent and exit
            StartActivity(mainIntent);
            Java.Lang.Runtime.GetRuntime().Exit(0);
        }

        /// <summary>
        /// Inflates a new fragment.
        /// </summary>
        /// <param name="fragment">The new <see cref="Fragment"/> to be inflated.</param>
        /// <param name="resourceId">The integer ID of the fragment resource.</param>
        public void InflateFragment(Fragment fragment, int resourceId)
        {
            // Add the new fragment
            SupportFragmentManager.BeginTransaction()
                .Add(resourceId, fragment)
                .Commit();
        }

        /// <summary>
        /// Hides an existing fragment.
        /// </summary>
        /// <param name="fragment">The <see cref="Fragment"/> to hide.</param>
        public void HideFragment(Fragment fragment)
        {
            SupportFragmentManager.BeginTransaction()
                .Hide(fragment)
                .Commit();
        }

        /// <summary>
        /// Shows an existing fragment.
        /// </summary>
        /// <param name="fragment">The <see cref="Fragment"/> to show.</param>
        public void ShowFragment(Fragment fragment)
        {
            SupportFragmentManager.BeginTransaction()
                .Show(fragment)
                .Commit();
        }

        /// <summary>
        /// Opens the <see cref="CurrentQueueFragment"/>.
        /// </summary>
        public void OpenCurrentQueueFragment()
        {
            bottomNavigation.SelectedItemId = Resource.Id.navigation_current_queue;
        }

        /// <summary>
        /// Starts the seekbar synchronization timer so the seekbar will update periodically based on current track progress.
        /// </summary>
        public void StartSeekbarSyncTimer()
        {
            seekbarSyncTimer.Enabled = true;
        }

        /// <summary>
        /// Stops the seekbar synchronization times so the seekbar does not update based on current track progress.
        /// </summary>
        public void StopSeekbarSyncTimer()
        {
            seekbarSyncTimer.Enabled = false;
        }

        /// <summary>
        /// Moves the app to the background when the back button is pressed rather than exiting.
        /// </summary>
        public override void OnBackPressed()
        {
            MoveTaskToBack(true);
        }

        /// <summary>
        /// Reinitializes the animation state of the expanded player when the screen is rotated.
        /// </summary>
        /// <param name="newConfig">The new <see cref="Configuration"/> of the screen.</param>
        public override void OnConfigurationChanged(Configuration newConfig)
        {
            presenter.UpdateExpandedPanelAfterRotation(panelLayout.GetPanelState());
            base.OnConfigurationChanged(newConfig);
        }

        /// <summary>
        /// Handles permission requests for the app.
        /// </summary>
        /// <param name="requestCode">Integer request code.</param>
        /// <param name="permissions">String array of permissions.</param>
        /// <param name="grantResults">Result of the permissions grant.</param>
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        /// <summary>
        /// Handles activity results for the app.
        /// </summary>
        /// <param name="requestCode">A request code indicating the caller of the activity with the result.</param>
        /// <param name="resultCode">A result code indicating the outcome of the process.</param>
        /// <param name="data">The result data.</param>
        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            presenter.HandleActivityResult(requestCode, resultCode, data);
            base.OnActivityResult(requestCode, resultCode, data);
        }

        /// <summary>
        /// Creates an instance of the <see cref="MainActivity"/>.
        /// </summary>
        /// <param name="savedInstanceState">A <see cref="Bundle"/> representing the previousy saved state.</param>
        protected override void OnCreate(Bundle savedInstanceState)
        {
            // Initialize activity
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            // Connect music browser and register callback
            MusicBrowser.Connect();
            MusicBrowser.RegisterMediaControllerCallback(mediaControllerCallback);

            // Register network change callback
            connectivityManager.RegisterDefaultNetworkCallback(networkChangeCallback);

            // Initialize view
            SetContentView(Resource.Layout.activity_main);

            // Initialize navigation bar
            bottomNavigation = FindViewById<BottomNavigationView>(Resource.Id.navigation_bar);
            bottomNavigation.SetOnItemSelectedListener(this);

            // Initialize sliding layout panel
            panelLayout = FindViewById<SlidingUpPanelLayout>(Resource.Id.sliding_layout);
            panelLayout.AddPanelSlideListener(this);

            // Initalize theme manager
            ThemeManager = new ThemeManager(Application.Context);

            // Initialize image loading service with very long timeout to disable the exception that is thrown after timeout
            var config = new FFImageLoading.Config.Configuration
            {
                HttpHeadersTimeout = 3600,
            };

            ImageService.Instance.Initialize(config);

            // Set timer parameters
            seekbarSyncTimer.Interval = 50;
            seekbarSyncTimer.Elapsed += OnUpdateSeekbarProgressesEvent;

            // Initialize home and player fragments
            presenter.InitializeMainFragments();
        }

        /// <summary>
        /// Called on the start of the <see cref="MainActivity"/>.
        /// </summary>
        protected override void OnStart()
        {
            base.OnStart();

            AppVisible = true;

            // Run initialization for start up
            presenter.InitializeOnStart(activityInitialized, panelLayout.GetPanelState());
        }

        /// <summary>
        /// Called on the stop of the <see cref="MainActivity"/>.
        /// </summary>
        protected override void OnStop()
        {
            base.OnStop();

            AppVisible = false;
        }

        /// <summary>
        /// Destroys the <see cref="MainActivity"/> and disconnects the <see cref="MusicBrowser"/>.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Unregister media controller callback and disconnect music browser
            MusicBrowser.UnregisterMediaControllerCallback();
            MusicBrowser.Disconnect();

            // Unregister the network change callback
            connectivityManager.UnregisterNetworkCallback(networkChangeCallback);

            // Unset activity initialized flag
            activityInitialized = false;
        }

        /// <summary>
        /// Handles intents sent to the activity.
        /// </summary>
        /// <param name="intent">An <see cref="Intent"/> sent to the activity.</param>
        protected override void OnNewIntent(Intent intent)
        {
            presenter.HandleIntent(intent);
        }

        /// <summary>
        /// Event triggered when the seekbar synchronization timer is updated.
        /// </summary>
        /// <param name="sender">Sender object.</param>
        /// <param name="e">Event args.</param>
        private void OnUpdateSeekbarProgressesEvent(object sender, System.EventArgs e)
        {
            presenter.UpdateSeekbarProgresses();
        }
    }
}