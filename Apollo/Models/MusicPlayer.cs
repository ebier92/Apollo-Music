// <copyright file="MusicPlayer.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Media;
using Android.Media.Session;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Source;
using Com.Google.Android.Exoplayer2.Upstream;
using AudioAttributes = Com.Google.Android.Exoplayer2.Audio.AudioAttributes;

namespace Apollo
{
    internal class MusicPlayer : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener, IPlayerEventListener
    {
        private const float VolumeDuck = 0.2f;
        private const float VolumeNormal = 1.0f;
        private readonly MusicService musicService;
        private readonly AudioManager audioManager;
        private readonly PowerManager.WakeLock wakeLock;
        private readonly WifiManager.WifiLock wifiLock;
        private readonly IntentFilter audioNoisyIntentFilter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy);
        private readonly BroadcastReceiver audioNoisyReceiver = new BroadcastReceiver();
        private readonly ICallback callback;
        private AudioFocusState audioFocusState = AudioFocusState.AudioNoFocusNoDuck;
        private AudioFocusRequestClass audioFocusRequestClass;
        private SimpleExoPlayer mediaPlayer;
        private PlaybackStateCode playbackState;
        private volatile bool audioNoisyReceiverRegistered;
        private volatile int currentPosition;
        private bool resumePlayOnFocus;

        /// <summary>
        /// Initializes a new instance of the <see cref="MusicPlayer"/> class.
        /// </summary>
        /// <param name="musicService">An instance of the <see cref="MusicService"/>.</param>
        public MusicPlayer(MusicService musicService)
        {
            // Set up service, audio manager, and Wifi lock
            this.musicService = musicService;
            callback = musicService;
            audioManager = (AudioManager)musicService.GetSystemService(Context.AudioService);
            wakeLock = ((PowerManager)Application.Context.GetSystemService("power")).NewWakeLock(WakeLockFlags.Partial, "Apollo_Player_Wake_Lock");
            wifiLock = ((WifiManager)musicService.GetSystemService(Context.WifiService)).CreateWifiLock(WifiMode.Full, "Apollo_Player_Wifi_Lock");
            playbackState = PlaybackStateCode.None;

            // Pause the audio if headphones get disconnected and the service is playing or buffering
            audioNoisyReceiver.OnReceiveAction = (context, intent) =>
            {
                if (intent.Action == AudioManager.ActionAudioBecomingNoisy && (PlaybackState == PlaybackStateCode.Playing || PlaybackState == PlaybackStateCode.Buffering))
                {
                    Pause();
                }
            };
        }

        /// <summary>
        /// Represents the current audio focus state.
        /// </summary>
        public enum AudioFocusState
        {
            /// <summary>
            /// Audio does not have focus and can not duck.
            /// </summary>
            AudioNoFocusNoDuck,

            /// <summary>
            /// Audio does not have focus but can duck.
            /// </summary>
            AudioNoFocusCanDuck,

            /// <summary>
            /// Audio has focus.
            /// </summary>
            AudioFocused,
        }

        /// <summary>
        /// Interface for callbacks on the <see cref="MusicPlayer"/>.
        /// </summary>
        public interface ICallback
        {
            /// <summary>
            /// Called when the <see cref="MusicPlayer"/> completes a track.
            /// </summary>
            void OnCompletion();

            /// <summary>
            /// Called when the <see cref="MusicPlayer"/> changes playback state.
            /// </summary>
            /// <param name="state">The new <see cref="PlaybackStateCode"/>.</param>
            void OnPlaybackStateChanged(PlaybackStateCode state);

            /// <summary>
            /// Called when an error occurs.
            /// </summary>
            /// <param name="error">Error description.</param>
            void OnError(string error);
        }

        public PlaybackStateCode PlaybackState
        {
            get
            {
                return playbackState;
            }

            set
            {
                // Set value and send callback if playback state value has changed
                if (callback != null)
                {
                    playbackState = value;
                    callback.OnPlaybackStateChanged(playbackState);
                }
            }
        }

        public int CurrentPosition
        {
            get
            {
                if (mediaPlayer != null)
                    return (int)mediaPlayer.CurrentPosition;
                else
                    return 0;
            }
        }

        public int Duration
        {
            get
            {
                if (mediaPlayer != null)
                    return (int)mediaPlayer.Duration;
                else
                    return 0;
            }
        }

        public bool PlayWhenReady
        {
            get
            {
                return mediaPlayer != null && mediaPlayer.PlayWhenReady;
            }

            set
            {
                if (mediaPlayer != null)
                    mediaPlayer.PlayWhenReady = value;
            }
        }

        public bool IsPlaying
        {
            get
            {
                if (mediaPlayer != null)
                    return resumePlayOnFocus || mediaPlayer.IsPlaying;
                else
                    return resumePlayOnFocus;
            }
        }

        /// <summary>
        /// Updates the audio focus state based on a new <see cref="AudioFocus"/>.
        /// </summary>
        /// <param name="focusChange"><see cref="AudioFocus"/> enum representing the current audio focus state.</param>
        public void OnAudioFocusChange(AudioFocus focusChange)
        {
            if (focusChange == AudioFocus.Gain)
            {
                audioFocusState = AudioFocusState.AudioFocused;
            }
            else if (focusChange == AudioFocus.Loss || focusChange == AudioFocus.LossTransient || focusChange == AudioFocus.LossTransientCanDuck)
            {
                // Set whether or not the focus state can duck
                audioFocusState = focusChange == AudioFocus.LossTransientCanDuck ? AudioFocusState.AudioNoFocusCanDuck : AudioFocusState.AudioNoFocusNoDuck;

                // Set whether the playback should resume on focus if the variable is not already set
                resumePlayOnFocus |= PlaybackState == PlaybackStateCode.Playing;
            }

            ConfigureMediaPlayerState();
        }

        /// <summary>
        /// Triggers the appropriate callback when the current track completes.
        /// </summary>
        /// <param name="playWhenReady">True if the player is ready.</param>
        /// <param name="playbackState">The current playback state.</param>
        public void OnPlayerStateChanged(bool playWhenReady, int playbackState)
        {
            if (playbackState == IPlayer.StateEnded && CurrentPosition > 0 && callback != null)
            {
                callback.OnCompletion();
            }
        }

        /// <summary>
        /// Configures the media player and triggers callbacks when the track begins playing.
        /// </summary>
        /// <param name="playing">The state of whether a track is playing.</param>
        public void OnIsPlayingChanged(bool playing)
        {
            if (playing)
            {
                ConfigureMediaPlayerState();

                // Set playback state change to playing only if not coming back from paused, since buffering will be set in that case
                if (PlaybackState != PlaybackStateCode.Paused)
                    PlaybackState = PlaybackStateCode.Playing;
            }
        }

        /// <summary>
        /// Updates the current track position after a seek operation is completed and sets playback state to buffering.
        /// </summary>
        public void OnSeekProcessed()
        {
            currentPosition = (int)mediaPlayer.CurrentPosition;
            PlaybackState = PlaybackStateCode.Buffering;
        }

        /// <summary>
        /// Triggers the appropriate callback when an error occurs.
        /// </summary>
        /// <param name="error">The specific <see cref="ExoPlaybackException"/> that occured.</param>
        public void OnPlayerError(ExoPlaybackException error)
        {
            if (callback != null)
                callback.OnError(string.Format(Application.Context.Resources.GetString(Resource.String.error_media_player), error, null));
        }

        /// <summary>
        /// Begins playing a <see cref="MediaSession.QueueItem"/>.
        /// </summary>
        /// <param name="streamUrlTask">A task to retrieve the content stream URL.</param>
        /// <returns>A <see cref="Task"/> containing a bool value representing whether or not the playing of the item was successful.</returns>
        public async Task<bool> LoadStream(Task<string> streamUrlTask)
        {
            // Aquire a wake lock if not held
            if (!wakeLock.IsHeld)
                wakeLock.Acquire();

            // Aquire a wifi lock if not held
            if (!wifiLock.IsHeld)
                wifiLock.Acquire();

            // Get the URL for the track stream
            var streamUrl = await streamUrlTask;

            // Return false
            if (streamUrl == null)
                return false;

            // Request focus, register the receiver, and configure the media player
            RequestAudioFocus();
            RegisterAudioNoisyReceiver();
            ConfigureMediaPlayerState();

            // Reset position
            currentPosition = 0;

            // Create a media player if one is needed
            if (mediaPlayer == null)
            {
                // Initialize player and add listener
                mediaPlayer = new SimpleExoPlayer.Builder(Application.Context).Build();
                mediaPlayer.AddListener(this);
            } // Reset the media player if it doesn't need to be created
            else
            {
                mediaPlayer.Stop();
            }

            // Set player audio attributes
            mediaPlayer.AudioAttributes = new AudioAttributes.Builder()
                .SetUsage(C.UsageMedia)
                .SetContentType(C.ContentTypeMusic)
                .Build();

            // Create media source and prepare for playback
            var mediaSource = BuildMediaSource(Android.Net.Uri.Parse(streamUrl));
            mediaPlayer.Prepare(mediaSource);

            // Return true if everything was successful
            return true;
        }

        /// <summary>
        /// Resumes playback if the current track is paused.
        /// </summary>
        public void Resume()
        {
            // Resume playback if the playback was paused
            if (mediaPlayer != null && PlaybackState == PlaybackStateCode.Paused)
            {
                RequestAudioFocus();
                RegisterAudioNoisyReceiver();
                ConfigureMediaPlayerState();

                // Perform a seek if the user changed the seekbar position while paused, otherwise set the playback state to
                // buffering to trigger the proper state flow from paused
                if (CurrentPosition != currentPosition)
                    mediaPlayer.SeekTo(currentPosition);
                else
                    PlaybackState = PlaybackStateCode.Buffering;

                mediaPlayer.PlayWhenReady = true;
            } // Call stop if media player was null when attempting to resume
            else if (mediaPlayer == null)
            {
                Stop();
            }
        }

        /// <summary>
        /// Formal pausing of playback. Releases held resources and unregisters the audio noisy receiver.
        /// </summary>
        public void Pause()
        {
            // Pause player, update track position, and release resources if currently playing or buffering
            if (PlaybackState == PlaybackStateCode.Playing || PlaybackState == PlaybackStateCode.Buffering)
            {
                if (mediaPlayer != null)
                {
                    mediaPlayer.PlayWhenReady = false;
                    currentPosition = CurrentPosition;
                }

                ReleaseAudioFocus();
            }

            // Set state and unregister receiver
            PlaybackState = PlaybackStateCode.Paused;
            UnregisterAudioNoisyReceiver();
        }

        /// <summary>
        /// Pauses playback without a formal resource release or change in playback state. Used to stop audio for short periods, such as between skipping songs.
        /// </summary>
        public void QuickPause()
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.PlayWhenReady = false;
                currentPosition = CurrentPosition;
            }
        }

        /// <summary>
        /// Seeks to a specified position in the current track.
        /// </summary>
        /// <param name="position">Track position expressed as an integer.</param>
        public void SeekTo(int position)
        {
            // Save the position for later reference if the media player is null or playback is paused
            if (mediaPlayer == null || PlaybackState == PlaybackStateCode.Paused)
                currentPosition = position;
            else // Seek to the requested position if the player is not null
                mediaPlayer.SeekTo(position);
        }

        /// <summary>
        /// Stops playback.
        /// </summary>
        public void Stop()
        {
            // Set playback state and position
            PlaybackState = PlaybackStateCode.Stopped;
            currentPosition = CurrentPosition;

            // Releases
            if (wakeLock.IsHeld)
                wakeLock.Release();

            if (wifiLock.IsHeld)
                wifiLock.Release();

            ReleaseAudioFocus();
            ReleaseMediaPlayer();
        }

        /// <summary>
        /// Configures the current <see cref="MediaPlayer"/> based on the <see cref="AudioFocus"/> and <see cref="PlaybackState"/>.
        /// </summary>
        private void ConfigureMediaPlayerState()
        {
            // Pause if the player is currently playing, does not have focus, and can not duck
            if (audioFocusState == AudioFocusState.AudioNoFocusNoDuck && PlaybackState == PlaybackStateCode.Playing)
            {
                Pause();
            } // App has audio focus and needs to duck
            else if (audioFocusState == AudioFocusState.AudioNoFocusCanDuck && mediaPlayer != null)
            {
                // Lower volume to duck levels
                mediaPlayer.Volume = VolumeDuck;
            } // App has main audio focus
            else if (audioFocusState == AudioFocusState.AudioFocused && mediaPlayer != null)
            {
                // Raise volume to normal levels
                mediaPlayer.Volume = VolumeNormal;
            }

            // Resume playing if the app lost audio focus
            if (audioFocusState != AudioFocusState.AudioNoFocusNoDuck && resumePlayOnFocus)
            {
                // Check that media play is not null and is not currently playing
                if (mediaPlayer != null && !mediaPlayer.IsPlaying)
                {
                    // Seek to current position if changed
                    if (currentPosition != CurrentPosition)
                        mediaPlayer.SeekTo(currentPosition);

                    // Resume playback
                    mediaPlayer.PlayWhenReady = true;
                    PlaybackState = PlaybackStateCode.Playing;
                }

                // Unset resume play flag
                resumePlayOnFocus = false;
            }
        }

        /// <summary>
        /// Requests audio focus if the <see cref="MediaPlayer"/> does not currently have audio focus.
        /// </summary>
        private void RequestAudioFocus()
        {
            // Attempt to get audio focus if not currently focused
            if (audioFocusState != AudioFocusState.AudioFocused)
            {
                AudioFocusRequest audioFocusRequest;

                // Build and submit focus request for API 26 and higher
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    audioFocusRequestClass = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
                        .SetAudioAttributes(new Android.Media.AudioAttributes.Builder().SetLegacyStreamType(Android.Media.Stream.Music).Build())
                        .SetOnAudioFocusChangeListener(this)
                        .Build();
                    audioFocusRequest = audioManager.RequestAudioFocus(audioFocusRequestClass);
                } // Submit request through the audio manager for API 25 and lower
                else
                {
                    #pragma warning disable CS0618 // Type or member is obsolete
                    audioFocusRequest = audioManager.RequestAudioFocus(this, Android.Media.Stream.Music, AudioFocus.Gain);
                    #pragma warning restore CS0618 // Type or member is obsolete
                }

                // Set the focus state if focus was gained
                if (audioFocusRequest == AudioFocusRequest.Granted)
                    audioFocusState = AudioFocusState.AudioFocused;
            }
        }

        /// <summary>
        /// Releases the <see cref="MediaPlayer"/> audio focus if the app currently has focus.
        /// </summary>
        private void ReleaseAudioFocus()
        {
            // Attempt to release audio focus if currently focused
            if (audioFocusState == AudioFocusState.AudioFocused)
            {
                AudioFocusRequest audioFocusRequest;

                // Request to abandon previously held audio focus for API 26 and higher
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    audioFocusRequest = audioManager.AbandonAudioFocusRequest(audioFocusRequestClass);
                } // Submit request to abandon focus through the audio manager for API 25 and lower
                else
                {
                    #pragma warning disable CS0618 // Type or member is obsolete
                    audioFocusRequest = audioManager.AbandonAudioFocus(this);
                    #pragma warning restore CS0618 // Type or member is obsolete
                }

                // Set the focus state if focus was gained
                if (audioFocusRequest == AudioFocusRequest.Granted)
                    audioFocusState = AudioFocusState.AudioNoFocusNoDuck;
            }
        }

        /// <summary>
        /// Registers the noisy audio receiver, which handles changes in audio output (i.e. earbuds to speakers).
        /// </summary>
        private void RegisterAudioNoisyReceiver()
        {
            if (!audioNoisyReceiverRegistered)
            {
                musicService.RegisterReceiver(audioNoisyReceiver, audioNoisyIntentFilter);
                audioNoisyReceiverRegistered = true;
            }
        }

        /// <summary>
        /// Unregisters the noisy audio receiver.
        /// </summary>
        private void UnregisterAudioNoisyReceiver()
        {
            if (audioNoisyReceiverRegistered)
            {
                musicService.UnregisterReceiver(audioNoisyReceiver);
                audioNoisyReceiverRegistered = false;
            }
        }

        /// <summary>
        /// Takes down and destroys the <see cref="MediaPlayer"/>.
        /// </summary>
        private void ReleaseMediaPlayer()
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.Release();
                mediaPlayer = null;
            }
        }

        /// <summary>
        /// Creates an <see cref="IMediaSource"/> to be played by the media player.
        /// </summary>
        /// <param name="uri">The <see cref="Android.Net.Uri"/> of the stream to play.</param>
        /// <returns>An <see cref="IMediaSource"/>.</returns>
        private IMediaSource BuildMediaSource(Android.Net.Uri uri)
        {
            var dataSourceFactory = new DefaultDataSourceFactory(Application.Context, "Apollo");

            return new ProgressiveMediaSource.Factory(dataSourceFactory).CreateMediaSource(uri);
        }

        /// <summary>
        /// Broadcast receiver class.
        /// </summary>
        private class BroadcastReceiver : Android.Content.BroadcastReceiver
        {
            public Action<Context, Intent> OnReceiveAction { get; set; }

            public override void OnReceive(Context context, Intent intent)
            {
                OnReceiveAction(context, intent);
            }
        }
    }
}