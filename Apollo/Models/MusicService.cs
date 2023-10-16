// <copyright file="MusicService.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Media;
using Android.Media.Browse;
using Android.Media.Session;
using Android.Net;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using AndroidX.Palette.Graphics;
using FFImageLoading;
using Timer = System.Timers.Timer;

namespace Apollo
{
    /// <summary>
    /// Music service class to handle all playback and content modification.
    /// </summary>
    [Service(Exported = true)]
    [IntentFilter(new[] { "android.media.browse.MediaBrowserService" })]
    internal class MusicService : Android.Service.Media.MediaBrowserService, MusicPlayer.ICallback
    {
        private const int StopDelay = 3000;
        private const int GeneratePlaylistPages = 5;
        private readonly DelayedStopHandler delayedStopHandler;
        private readonly ConnectivityManager connectivityManager;
        private readonly NetworkStatus.NetworkChangeCallback networkChangeCallback;
        private readonly Timer trackProgressTimer;
        private MediaSession mediaSession;
        private MusicPlayer musicPlayer;
        private MediaSessionCallback mediaSessionCallback;
        private MusicQueue musicQueue;
        private QueueItemAsyncData queueItemData;
        private MediaNotificationManager mediaNotificationManager;
        private List<RecommendationsManager.SeedTrackData> seedTrackData;
        private CancellationTokenSource trackCancellationTokenSource;
        private CancellationTokenSource playlistCancellationTokenSource;
        private SettingsManager.PlaylistSourceSettingOptions playlistSource;
        private bool serviceRunning;
        private bool resumePlaybackOnNetworkAvailable;
        private string playlistVideoId;
        private string continuationToken;
        private string visitorData;

        /// <summary>
        /// Initializes a new instance of the <see cref="MusicService"/> class.
        /// </summary>
        public MusicService()
        {
            delayedStopHandler = new DelayedStopHandler(Looper.MainLooper, this);
            connectivityManager = (ConnectivityManager)Application.Context.GetSystemService(ConnectivityService);
            networkChangeCallback = new NetworkStatus.NetworkChangeCallback();
            trackCancellationTokenSource = new CancellationTokenSource();
            playlistCancellationTokenSource = new CancellationTokenSource();
            trackProgressTimer = new Timer();

            // Network connectivity has become available
            networkChangeCallback.OnAvailableAction = network =>
            {
                OnNetworkAvailable();
            };

            // Network connectivity has been lost
            networkChangeCallback.OnLostAction = network =>
            {
                OnNetworkLost();
            };

            // Set up track progress timer
            trackProgressTimer.Interval = 2000;
            trackProgressTimer.Elapsed += OnUpdateTrackProgressEvent;
        }

        /// <summary>
        /// Gets a cancellation token to cancel track related tasks, such as meta data loading or playback.
        /// </summary>
        private CancellationToken TrackCancellationToken
        {
            get
            {
                // Return token from cancellation token source if available, otherwise return an empty token
                if (trackCancellationTokenSource != null)
                    return trackCancellationTokenSource.Token;
                else
                    return CancellationToken.None;
            }
        }

        /// <summary>
        /// Gets a cancellation token to cancel playlist related tasks, such as playlist loading or playlist generation.
        /// </summary>
        private CancellationToken PlaylistCancellationToken
        {
            get
            {
                // Return token from cancellation token source if available, otherwise return an empty token.
                if (playlistCancellationTokenSource != null)
                    return playlistCancellationTokenSource.Token;
                else
                    return CancellationToken.None;
            }
        }

        /// <inheritdoc cref="ContentManager.CreateTrackMediaId(string, string)"/>
        public static string CreateTrackMediaId(string playlistName, string trackUrl)
        {
            return ContentManager.CreateTrackMediaId(playlistName, trackUrl);
        }

        /// <inheritdoc cref="ContentManager.CreatePlaylistMediaId(string)"/>
        public static string CreatePlaylistMediaId(string playlistName)
        {
            return ContentManager.CreatePlaylistMediaId(playlistName);
        }

        /// <inheritdoc cref="ContentManager.IsPlaylistAndExists(string)"/>
        public static bool IsPlaylistAndExists(string mediaId)
        {
            return ContentManager.IsPlaylistAndExists(mediaId);
        }

        /// <inheritdoc cref="ContentManager.GetPlaylistNameFromMediaId(string)"/>
        public static string GetPlaylistNameFromMediaId(string mediaId)
        {
            return ContentManager.GetPlaylistNameFromMediaId(mediaId);
        }

        /// <summary>
        /// Initializes the media session, player, UI intent, and control actions.
        /// </summary>
        public override void OnCreate()
        {
            base.OnCreate();

            mediaSession = new MediaSession(this, "MusicService");
            musicQueue = new MusicQueue();
            mediaSessionCallback = new MediaSessionCallback();

            // Initialize JSON files if they do not yet exist
            ContentManager.InitializeJsonFile();
            SettingsManager.InitializeJsonFile();
            RecommendationsManager.InitializeJsonFile();

            // Initialize media session
            mediaSession.SetCallback(mediaSessionCallback);
            mediaSession.SetFlags(MediaSessionFlags.HandlesMediaButtons | MediaSessionFlags.HandlesTransportControls);
            SessionToken = mediaSession.SessionToken;

            // Initialize music player
            musicPlayer = new MusicPlayer(this);

            // Register network change callback
            connectivityManager.RegisterDefaultNetworkCallback(networkChangeCallback);

            // Set intent for launching the app UI
            var intent = new Intent(ApplicationContext, typeof(MainActivity));
            var pendingIntent = PendingIntent.GetActivity(ApplicationContext, 99, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            mediaSession.SetSessionActivity(pendingIntent);

            // Initialize playback state
            UpdateMediaSessionPlaybackState(null);

            // Pause
            mediaSessionCallback.OnPauseAction = () =>
            {
                RequestPause();
            };

            // Play
            mediaSessionCallback.OnPlayAction = () =>
            {
                if (musicQueue != null && musicQueue.QueueLength > 0)
                    RequestPlay();
            };

            // Open playlist or play track by media ID
            mediaSessionCallback.OnPlayFromMediaIdAction = (mediaId, extras) =>
            {
                PlayFromMediaId(mediaId);
            };

            // Seek to a specified track position
            mediaSessionCallback.OnSeekToAction = (pos) =>
            {
                musicPlayer.SeekTo((int)pos);
            };

            // Skip to the next track
            mediaSessionCallback.OnSkipToNextAction = () =>
            {
                SkipToNext();
            };

            // Skip to the previous track
            mediaSessionCallback.OnSkipToPreviousAction = () =>
            {
                SkipToPrevious();
            };

            // Skip to a specified queue item
            mediaSessionCallback.OnSkipToQueueItemAction = (queueId) =>
            {
                SkipToQueueItem(queueId);
            };

            // Stop
            mediaSessionCallback.OnStopAction = () =>
            {
                RequestStop();
            };

            // Handle custom commands
            mediaSessionCallback.OnCommandAction = (string command, Bundle args, ResultReceiver cb) =>
            {
                CallCommand(command, args, cb);
            };

            // Create notification manager by passing in the service after initialization
            mediaNotificationManager = new MediaNotificationManager(this);
        }

        /// <summary>
        /// Returns the content browser root for a browser needing access to media content.
        /// </summary>
        /// <param name="clientPackageName">Name of the client package.</param>
        /// <param name="clientUid">Integer client UID.</param>
        /// <param name="rootHints">Bundle containing additional root info.</param>
        /// <returns>The <see cref="MediaBrowser.BrowserRoot"/> of the media content.</returns>
        public override BrowserRoot OnGetRoot(string clientPackageName, int clientUid, Bundle rootHints)
        {
            return new BrowserRoot(ContentManager.RootId, null);
        }

        /// <summary>
        /// Gets children <see cref="MediaBrowser.MediaItem"/>s and sends back the result to the browser.
        /// </summary>
        /// <param name="parentId">Media ID of the parent object.</param>
        /// <param name="result"><see cref="Result"/> object to send data back to the browser.</param>
        public override void OnLoadChildren(string parentId, Result result)
        {
            // Get media items under parent ID
            var mediaItems = ContentManager.GetMediaItems(parentId);

            // Send the result if the list of media items was not empty
            result.SendResult(new JavaList<MediaBrowser.MediaItem>(mediaItems));
        }

        /// <summary>
        /// Called when the service is destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            RequestStop();
            mediaSession.Release();
            base.OnDestroy();
        }

        /// <summary>
        /// Called when a track completes playback.
        /// </summary>
        public void OnCompletion()
        {
            // Move to next track and play
            if (musicQueue != null && musicQueue.QueueLength > 0)
            {
                // Set playback state, pause playback, and reset position
                musicPlayer.PlaybackState = PlaybackStateCode.SkippingToNext;
                musicPlayer.SeekTo(0);
                musicPlayer.QuickPause();

                // Stop playback if at the end of the queue, otherwise play the next track in the queue
                if (musicQueue.QueueIndex == musicQueue.QueueLength - 1)
                {
                    RequestStop();
                }
                else
                {
                    musicQueue.IncrementIndex();
                    RequestPlay();
                }
            } // Queue is empty, request stop
            else
            {
                RequestStop();
            }
        }

        /// <summary>
        /// Called when the <see cref="PlaybackState"/> changes.
        /// </summary>
        /// <param name="playbackState">The new <see cref="PlaybackState"/>.</param>
        public void OnPlaybackStateChanged(PlaybackStateCode playbackState)
        {
            UpdateMediaSessionPlaybackState(null);
            UpdateTimerEnabled(playbackState);
        }

        /// <summary>
        /// Updates the <see cref="PlaybackState"/> with an error code if an error occurs.
        /// </summary>
        /// <param name="error">String error code.</param>
        public void OnError(string error)
        {
            // Handle loss of network, otherwise set standard error state
            if (error.Contains("HttpDataSourceException"))
            {
                musicPlayer.QuickPause();
                musicPlayer.PlaybackState = PlaybackStateCode.Buffering;
                resumePlaybackOnNetworkAvailable = true;
            }
            else
            {
                UpdateMediaSessionPlaybackState(error);
            }
        }

        /// <summary>
        /// Called when network connectivity becomes available.
        /// </summary>
        public async void OnNetworkAvailable()
        {
            if (musicPlayer != null && musicPlayer.PlaybackState == PlaybackStateCode.Buffering && resumePlaybackOnNetworkAvailable)
            {
                // Save initial track position
                var seekPosition = musicPlayer.CurrentPosition;

                // Set playback state code to stopped, then play from network recovery and seek to the last position
                musicPlayer.PlaybackState = PlaybackStateCode.Stopped;
                await RequestPlayAwaitable();
                musicPlayer.SeekTo(seekPosition);
            }

            resumePlaybackOnNetworkAvailable = false;
        }

        /// <summary>
        /// Called when network connectivity is lost.
        /// </summary>
        public void OnNetworkLost()
        {
            // Cancel all network dependent tasks
            trackCancellationTokenSource.Cancel();
            playlistCancellationTokenSource.Cancel();

            // Dispose and recreate the cancellation token sources for future use
            trackCancellationTokenSource.Dispose();
            playlistCancellationTokenSource.Dispose();
            trackCancellationTokenSource = new CancellationTokenSource();
            playlistCancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Enables or disables the track progress timer based on whether the track is playing or not.
        /// </summary>
        /// <param name="playbackState">The current <see cref="PlaybackStateCode"/>.</param>
        private void UpdateTimerEnabled(PlaybackStateCode playbackState)
        {
            if (playbackState == PlaybackStateCode.Playing)
                trackProgressTimer.Enabled = true;
            else
                trackProgressTimer.Enabled = false;
        }

        /// <summary>
        /// Event triggered when the track progress timer is updated.
        /// </summary>
        /// <param name="sender">Sender object.</param>
        /// <param name="e">Event args.</param>
        private void OnUpdateTrackProgressEvent(object sender, EventArgs e)
        {
            // Attempt loading for the next track if there are less than 60 seconds remaining and there is at least one more track in the playlist
            if (musicPlayer.Duration - musicPlayer.CurrentPosition <= 60000 && musicQueue.QueueIndex < musicQueue.QueueLength - 1)
            {
                var nextQueueItem = musicQueue.GetItem(musicQueue.QueueIndex + 1);
                var nextQueueItemMediaId = nextQueueItem.Description.MediaId;
                var queueItemDataMediaId = queueItemData != null ? queueItemData.QueueItem.Description.MediaId : "";

                // Load queue item async data if the media ID values of the next queue item versus the loaded queue item (if any) do not match
                if (nextQueueItemMediaId != queueItemDataMediaId)
                    queueItemData = new QueueItemAsyncData(nextQueueItem, TrackCancellationToken);
            }
        }

        /// <summary>
        /// Loads a playlist or plays a track represented by a media ID.
        /// </summary>
        /// <param name="mediaId">The content media ID.</param>
        private void PlayFromMediaId(string mediaId)
        {
            // If the media ID is a valid playlist, load tracks to the queue
            if (ContentManager.IsPlaylist(mediaId))
            {
                var playlistMediaItems = ContentManager.GetMediaItems(mediaId);

                if (playlistMediaItems.Count > 0)
                {
                    // Request stop if needed
                    if (musicPlayer.PlaybackState != PlaybackStateCode.None && musicPlayer.PlaybackState != PlaybackStateCode.Stopped)
                        RequestStop();

                    // Get queue title from first media ID
                    var playlistTitle = ContentManager.GetPlaylistNameFromMediaId(playlistMediaItems[0].MediaId);

                    // Reinitialize music queue
                    musicQueue = new MusicQueue
                    {
                        // Load queue and set queue to media session
                        Queue = ContentManager.ConvertMediaItemsToQueue(playlistMediaItems),
                    };

                    mediaSession.SetQueue(musicQueue.Queue);
                    mediaSession.SetQueueTitle(playlistTitle);

                    // Add first track to recommendation data
                    var description = musicQueue.Queue[0].Description;
                    RecommendationsManager.AddTrack(description.Title, description.Subtitle, description.Extras.GetLong("Duration"), description.MediaUri.ToString());
                }
            } // If the media ID is a valid track, attempt to play it from the currently loaded playlist
            else if (ContentManager.IsTrack(mediaId))
            {
                if (musicQueue != null && musicQueue.QueueLength > 0)
                {
                    var queueItem = musicQueue.GetItem(mediaId);

                    // Continue if queue item was successfully found
                    if (queueItem != null)
                    {
                        // Request stop if needed
                        if (musicPlayer.PlaybackState != PlaybackStateCode.None && musicPlayer.PlaybackState != PlaybackStateCode.Stopped)
                            RequestStop();

                        musicQueue.SetItemByQueueId(queueItem.QueueId);
                        RequestPlay();

                        // Add selected track to recommendation data
                        var description = queueItem.Description;
                        RecommendationsManager.AddTrack(description.Title, description.Subtitle, description.Extras.GetLong("Duration"), description.MediaUri.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Skips to the next track in the queue.
        /// </summary>
        private void SkipToNext()
        {
            if (musicQueue != null && musicQueue.QueueLength > 0 && !resumePlaybackOnNetworkAvailable)
            {
                // Pause if playing without a formal state change and move back to position 0
                if (musicPlayer.PlaybackState == PlaybackStateCode.Playing)
                {
                    musicPlayer.QuickPause();
                    musicPlayer.SeekTo(0);
                }

                musicPlayer.PlaybackState = PlaybackStateCode.SkippingToNext;
                musicQueue.IncrementIndex();
                RequestPlay();
            }
        }

        /// <summary>
        /// Skips to the previous track in the queue.
        /// </summary>
        private void SkipToPrevious()
        {
            if (musicQueue != null && musicQueue.QueueLength > 0 && !resumePlaybackOnNetworkAvailable)
            {
                // Pause if playing without a formal state change and move back to position 0
                if (musicPlayer.PlaybackState == PlaybackStateCode.Playing)
                {
                    musicPlayer.QuickPause();
                    musicPlayer.SeekTo(0);
                }

                musicPlayer.PlaybackState = PlaybackStateCode.SkippingToPrevious;
                musicQueue.DecrementIndex();
                RequestPlay();
            }
        }

        /// <summary>
        /// Skips to a specific item in the queue based on a queue ID value.
        /// </summary>
        /// <param name="queueId">The queue ID of the queue item to skip to.</param>
        private void SkipToQueueItem(long queueId)
        {
            if (musicQueue != null && musicQueue.QueueLength > 0 && !resumePlaybackOnNetworkAvailable)
            {
                // Pause if playing without a formal state change and move back to position 0
                if (musicPlayer.PlaybackState == PlaybackStateCode.Playing)
                {
                    musicPlayer.QuickPause();
                    musicPlayer.SeekTo(0);
                }

                musicPlayer.PlaybackState = PlaybackStateCode.SkippingToQueueItem;
                musicQueue.SetItemByQueueId(queueId);
                RequestPlay();
            }
        }

        /// <summary>
        /// Pauses playback.
        /// </summary>
        private void RequestPause()
        {
            // Do not allow pauses if the network is disconnected and the resume play flag is set
            if (!NetworkStatus.IsConnected && resumePlaybackOnNetworkAvailable)
                return;

            musicPlayer.Pause();
            delayedStopHandler.RemoveCallbacksAndMessages(null);
            delayedStopHandler.SendEmptyMessageDelayed(0, StopDelay);
        }

        /// <summary>
        /// Sets up the service and music player for track playback.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task RequestPlayAwaitable()
        {
            // Show an error message and exit if there is no network connectivity
            if (!NetworkStatus.IsConnected)
            {
                Toast.MakeText(Application.Context, Resource.String.error_no_network, ToastLength.Short).Show();

                return;
            }

            // Get cancellation token
            var cancellationToken = TrackCancellationToken;

            // Activate media session if not active
            if (!mediaSession.Active)
                mediaSession.Active = true;

            // Resume playback if paused and exit
            if (musicPlayer.PlaybackState == PlaybackStateCode.Paused)
            {
                // UpdatePlaybackState called automatically here because IsPlaying will change
                musicPlayer.Resume();

                return;
            }

            // Start loading queue item async data if it has not been loaded yet
            var currentQueueItem = musicQueue.GetCurrentItem();

            if (queueItemData == null || queueItemData.QueueItem.Description.MediaId != currentQueueItem.Description.MediaId)
                queueItemData = new QueueItemAsyncData(currentQueueItem, cancellationToken);

            // Update session metadata
            MediaMetadata mediaMetadata;

            try
            {
                mediaMetadata = await queueItemData.MediaMetadata;
            } // Operation was canceled
            catch (System.OperationCanceledException)
            {
                // Show network error message if cancellation was due to network drop and stop playback
                if (!NetworkStatus.IsConnected)
                {
                    Toast.MakeText(Application.Context, Resource.String.error_no_network, ToastLength.Short).Show();
                    RequestStop();
                }

                return;
            }
            catch
            {
                Toast.MakeText(Application.Context, Resource.String.error_playing_track, ToastLength.Short).Show();

                // Request stop and exit
                RequestStop();

                return;
            }

            // If metadata was found, set it for the session otherwise set an error code playback state
            if (mediaMetadata != null)
                mediaSession.SetMetadata(mediaMetadata);
            else
                UpdateMediaSessionPlaybackState(Resources.GetString(Resource.String.error_no_metadata));

            // Update playbackstate to buffering
            musicPlayer.PlaybackState = PlaybackStateCode.Buffering;

            // Load the stream
            bool streamLoadSuccessful;

            try
            {
                streamLoadSuccessful = await musicPlayer.LoadStream(queueItemData.StreamUrl);
            } // Operation was canceled
            catch (System.OperationCanceledException)
            {
                // Show network error message if cancellation was due to network drop and stop playback
                if (!NetworkStatus.IsConnected)
                {
                    Toast.MakeText(Application.Context, Resource.String.error_no_network, ToastLength.Short).Show();
                    RequestStop();
                }

                return;
            }
            catch
            {
                Toast.MakeText(Application.Context, Resource.String.error_playing_track, ToastLength.Short).Show();

                // Request stop and exit
                RequestStop();

                return;
            }

            // Play if stream load succeeded and playback state is not stopped or paused
            if (streamLoadSuccessful && musicPlayer.PlaybackState != PlaybackStateCode.Paused && musicPlayer.PlaybackState != PlaybackStateCode.Stopped)
            {
                // Start service if not running
                if (!serviceRunning)
                {
                    StartForegroundService(new Intent(ApplicationContext, typeof(MusicService)));
                    serviceRunning = true;
                }

                musicPlayer.PlayWhenReady = true;
            } // Player was paused or stopped during initial buffering
            else if (musicPlayer.PlaybackState == PlaybackStateCode.Paused || musicPlayer.PlaybackState == PlaybackStateCode.Stopped)
            {
                return;
            } // Stream could not be played due to some error
            else
            {
                // Display user message that track can not be played
                Toast.MakeText(Application.Context, Resource.String.track_could_not_be_played, ToastLength.Short).Show();

                // Move to the next track there are multiple items in the queue
                if (musicQueue != null && musicQueue.QueueLength > 1)
                {
                    musicPlayer.PlaybackState = PlaybackStateCode.SkippingToNext;
                    musicQueue.IncrementIndex();
                    RequestPlay();
                } // Stop playback if there is only one item in the queue
                else
                {
                    RequestStop();
                }
            }
        }

        /// <summary>
        /// Sets up the service and music player for track playback.
        /// </summary>
        private async void RequestPlay()
        {
            await RequestPlayAwaitable();
        }

        /// <summary>
        /// Stops track playback.
        /// </summary>
        private void RequestStop()
        {
            // Stop playback
            musicPlayer.Stop();

            // Cancel all in progess tasks
            trackCancellationTokenSource.Cancel();

            // Dispose and recreate the cancellation token source for future use
            trackCancellationTokenSource.Dispose();
            trackCancellationTokenSource = new CancellationTokenSource();

            // Clear any queue item data
            queueItemData = null;

            // Clear delayed stop handler
            delayedStopHandler.RemoveCallbacksAndMessages(null);
            delayedStopHandler.SendEmptyMessageDelayed(0, StopDelay);
        }

        /// <summary>
        /// Updates the current <see cref="PlaybackState"/> for the media session and current active queue item if applicable.
        /// </summary>
        /// <param name="error">Error code, if any.</param>
        private void UpdateMediaSessionPlaybackState(string error)
        {
            var stateBuilder = new PlaybackState.Builder().SetActions(GetAvailableActions());
            var playbackState = musicPlayer.PlaybackState;

            if (error != null)
            {
                stateBuilder.SetErrorMessage(error);
                playbackState = PlaybackStateCode.Error;
            }

            stateBuilder.SetState(playbackState, musicPlayer.CurrentPosition, 1.0f, SystemClock.ElapsedRealtime());

            // Set the active queue item ID if there are tracks loaded in the queue
            if (musicQueue.QueueLength > 0)
            {
                var item = musicQueue.GetItem(musicQueue.QueueIndex);
                stateBuilder.SetActiveQueueItemId(item.QueueId);
            }

            mediaSession.SetPlaybackState(stateBuilder.Build());

            if (playbackState == PlaybackStateCode.Playing || playbackState == PlaybackStateCode.Paused || playbackState == PlaybackStateCode.Buffering)
            {
                mediaNotificationManager.StartNotification();
            }
        }

        /// <summary>
        /// Returns the types of playback actions available at the current time.
        /// </summary>
        /// <returns>Available actions expressed as a long.</returns>
        private long GetAvailableActions()
        {
            long actions = PlaybackState.ActionPlay | PlaybackState.ActionPlayFromMediaId | PlaybackState.ActionPlayFromSearch;

            if (musicQueue.QueueLength == 0)
                return actions;

            // Can stop when playing or paused
            if (musicPlayer.PlaybackState == PlaybackStateCode.Paused || musicPlayer.PlaybackState == PlaybackStateCode.Playing)
                actions |= PlaybackState.ActionStop;

            // Can pause when playing
            if (musicPlayer.IsPlaying)
                actions |= PlaybackState.ActionPause;

            // Can skip to previous if player is playing or paused
            if (musicPlayer.PlaybackState == PlaybackStateCode.Paused || musicPlayer.PlaybackState == PlaybackStateCode.Playing)
                actions |= PlaybackState.ActionSkipToPrevious;

            // Can skip to next if player is playing or paused
            if (musicPlayer.PlaybackState == PlaybackStateCode.Paused || musicPlayer.PlaybackState == PlaybackStateCode.Playing)
                actions |= PlaybackState.ActionSkipToNext;

            return actions;
        }

        /// <summary>
        /// Call the appropriate service command with arguments based on the command name.
        /// </summary>
        /// <param name="command">The name of the command to execute.</param>
        /// <param name="args">The command aruments <see cref="Bundle"/>.</param>
        /// <param name="cb">The <see cref="ResultReceiver"/> to send back the results of the command.</param>
        private void CallCommand(string command, Bundle args, ResultReceiver cb)
        {
            // Call the appropriate method based on which command was sent
            switch (command)
            {
                case Commands.CmdEnableShuffleMode:
                    CommandEnableShuffleMode(cb);
                    break;
                case Commands.CmdDisableShuffleMode:
                    CommandDisableShuffleMode(cb);
                    break;
                case Commands.CmdSaveQueueItems:
                    CommandSaveQueueItems(args, cb);
                    break;
                case Commands.CmdDeleteMediaItems:
                    CommandDeleteMediaItems(args);
                    break;
                case Commands.CmdClearQueueItems:
                    CommandClearQueueItems();
                    break;
                case Commands.CmdMoveQueueItem:
                    CommandMoveQueueItem(args);
                    break;
                case Commands.CmdRemoveQueueItem:
                    CommandRemoveQueueItem(args);
                    break;
                case Commands.CmdQueueVideoToNewPlaylist:
                    CommandQueueVideoToNewPlaylist(args);
                    break;
                case Commands.CmdQueueVideoNext:
                    CommandQueueVideoNext(args);
                    break;
                case Commands.CmdQueueVideoLast:
                    CommandQueueVideoLast(args);
                    break;
                case Commands.CmdSaveVideoToPlaylist:
                    CommandSaveVideoToPlaylist(args);
                    break;
                case Commands.CmdLoadPlaylist:
                    CommandLoadPlaylist(args, cb);
                    break;
                case Commands.CmdGenerateNewPlaylist:
                    CommandGenerateNewPlaylist(args, cb);
                    break;
                case Commands.CmdContinueGeneratePlaylist:
                    CommandContinueGeneratePlaylist(cb);
                    break;
                case Commands.CmdGenerateRecommendedPlaylist:
                    CommandGenerateRecommendedPlaylist(cb);
                    break;
                case Commands.CmdContinueGenerateRecommendedPlaylist:
                    CommandContinueGenerateRecommendedPlaylist(cb);
                    break;
                case Commands.CmdGenerateHistoricalPlaylist:
                    CommandGenerateHistoricalPlaylist(cb);
                    break;
            }
        }

        /// <summary>
        /// Enables shuffle mode.
        /// <param name="resultReceiver">Callback to receive the result of the command.</param>
        /// </summary>
        private void CommandEnableShuffleMode(ResultReceiver resultReceiver)
        {
            // If the player is playing, paused, or buffering, add the current queue item to the beginning of the queue when shuffling, otherwise shuffle all items randomly
            if (musicPlayer.PlaybackState == PlaybackStateCode.Playing || musicPlayer.PlaybackState == PlaybackStateCode.Paused || musicPlayer.PlaybackState == PlaybackStateCode.Buffering)
                musicQueue.Shuffle(true);
            else
                musicQueue.Shuffle(false);

            // Load the queue into the session
            mediaSession.SetQueue(musicQueue.Queue);

            // Create bundle and send the command result
            var resultData = new Bundle();
            resultData.PutString("command", Commands.CmdEnableShuffleMode);
            resultReceiver.Send(Android.App.Result.Ok, resultData);
        }

        /// <summary>
        /// Disables shuffle mode.
        /// <param name="resultReceiver">Callback to receive the result of the command.</param>
        /// </summary>
        private void CommandDisableShuffleMode(ResultReceiver resultReceiver)
        {
            // Unshuffle the queue and load it to the session
            musicQueue.Unshuffle();
            mediaSession.SetQueue(musicQueue.Queue);

            // Create bundle and send the command result
            var resultData = new Bundle();
            resultData.PutString("command", Commands.CmdDisableShuffleMode);
            resultReceiver.Send(Android.App.Result.Ok, resultData);
        }

        /// <summary>
        /// Saves the current queue items to the content file using the specified parent media ID.
        /// </summary>
        /// <param name="args">Command argument <see cref="Bundle"/>.</param>
        /// /// <param name="resultReceiver">Callback to receive the result of the command.</param>
        private void CommandSaveQueueItems(Bundle args, ResultReceiver resultReceiver)
        {
            // Get command argument
            var parentMediaId = args.GetString("parentMediaId");

            // If the argument was retrieved, proceed with saving the queue
            if (parentMediaId != null)
            {
                // Convert to media items and save to the content file
                var mediaItems = ContentManager.ConvertQueueToMediaItems(musicQueue.Queue);
                ContentManager.SaveMediaItems(parentMediaId, mediaItems);

                // Save the current active queue index
                var queueIndex = musicQueue.QueueIndex;

                // Update the music queue from the newly saved content
                var queueItems = ContentManager.ConvertMediaItemsToQueue(ContentManager.GetMediaItems(parentMediaId));
                musicQueue.Queue = queueItems;

                // Reincrement the index to match the previous index
                while (musicQueue.QueueIndex != queueIndex)
                {
                    musicQueue.IncrementIndex();
                }

                // Extract the playlist title from the parent media ID
                var playlistTitle = ContentManager.GetPlaylistNameFromMediaId(parentMediaId);

                // Set the title and queue to the media session
                mediaSession.SetQueue(musicQueue.Queue);
                mediaSession.SetQueueTitle(playlistTitle);

                // Create bundle and send the command result
                var resultData = new Bundle();
                resultData.PutString("command", Commands.CmdSaveQueueItems);
                resultReceiver.Send(Android.App.Result.Ok, resultData);
            }
        }

        /// <summary>
        /// Delete a playlist from the content file by parent media ID, if the playlist exists.
        /// </summary>
        private void CommandDeleteMediaItems(Bundle args)
        {
            // Get the command argument
            var parentMediaId = args.GetString("parentMediaId");

            // If the argument was retrieved, delete the playlist
            if (parentMediaId != null)
            {
                ContentManager.DeleteMediaItems(parentMediaId);
            }
        }

        /// <summary>
        /// Stops playback and clears all items from the current queue.
        /// </summary>
        private void CommandClearQueueItems()
        {
            // Stop any current playback
            if (musicPlayer.PlaybackState != PlaybackStateCode.None && musicPlayer.PlaybackState != PlaybackStateCode.Stopped)
                RequestStop();

            // Reinitialize music queue to clear it
            musicQueue = new MusicQueue();

            // Set session queue to the empty music queue
            mediaSession.SetQueue(musicQueue.Queue);
        }

        /// <summary>
        /// Move a queue item from to a specified position using its media ID.
        /// </summary>
        /// <param name="args">Command argument <see cref="Bundle"/>.</param>
        private void CommandMoveQueueItem(Bundle args)
        {
            // Get command arguments
            var mediaId = args.GetString("mediaId");
            var toPosition = args.GetInt("toPosition", -1);

            // If the arguments were retrieved, move the item and refresh the queue
            if (mediaId != null && toPosition != -1)
            {
                musicQueue.MoveItem(mediaId, toPosition);
                mediaSession.SetQueue(musicQueue.Queue);
            }
        }

        /// <summary>
        /// Move a queue item from to a specified position using its media ID.
        /// </summary>
        /// <param name="args">Command argument <see cref="Bundle"/>.</param>
        private void CommandRemoveQueueItem(Bundle args)
        {
            // Get the command argument
            var position = args.GetInt("position", -1);

            // Check if the argument was retrieved
            if (position != -1)
            {
                // Check if the item being removed if the last queue item
                if (musicQueue.QueueLength == 1)
                {
                    // Stop playback, remove item, and reset the queue
                    if (musicPlayer.PlaybackState != PlaybackStateCode.None && musicPlayer.PlaybackState != PlaybackStateCode.Stopped)
                        RequestStop();

                    musicQueue.RemoveItem(position);
                    mediaSession.SetQueue(musicQueue.Queue);
                } // Check if the currently playing or buffering queue item is being removed
                else if (position == musicQueue.QueueIndex && (musicPlayer.PlaybackState == PlaybackStateCode.Playing || musicPlayer.PlaybackState == PlaybackStateCode.Buffering))
                {
                    // Stop playback, remove item, reset the queue, and start playback again
                    if (musicPlayer.PlaybackState != PlaybackStateCode.None && musicPlayer.PlaybackState != PlaybackStateCode.Stopped)
                        RequestStop();

                    musicQueue.RemoveItem(position);
                    mediaSession.SetQueue(musicQueue.Queue);
                    RequestPlay();
                } // If the current queue item is not being removed, just remove the item and reset the queue
                else
                {
                    musicQueue.RemoveItem(position);
                    mediaSession.SetQueue(musicQueue.Queue);
                }
            }
        }

        /// <summary>
        /// Start a new playlist using a single queue item.
        /// </summary>
        /// <param name="args">Command argument <see cref="Bundle"/>.</param>
        private void CommandQueueVideoToNewPlaylist(Bundle args)
        {
            // Get the command arguments
            var title = args.GetString("title");
            var artist = args.GetString("artist");
            var iconUri = args.GetString("iconUri");
            var mediaUri = args.GetString("mediaUri");
            var duration = args.GetLong("duration", -1);

            if (title != null && artist != null && iconUri != null && mediaUri != null && duration != -1)
            {
                // Stop any current playback
                if (musicPlayer.PlaybackState != PlaybackStateCode.None && musicPlayer.PlaybackState != PlaybackStateCode.Stopped)
                    RequestStop();

                // Build the queue item
                var queueItem = ContentManager.BuildQueueItem(
                    ContentManager.CreateTrackMediaId(ContentManager.DefaultPlaylistName, mediaUri),
                    duration,
                    mediaUri,
                    title,
                    artist,
                    iconUri,
                    0);

                // Set the queue item as the only item in the queue and set the queue for the session
                musicQueue.Queue = new List<MediaSession.QueueItem>() { queueItem };
                mediaSession.SetQueue(musicQueue.Queue);

                // Set queue title to unsaved playlist
                mediaSession.SetQueueTitle(GetString(Resource.String.unsaved_playlist));

                // Play the newly loaded item
                RequestPlay();

                // Add to recommendation data
                RecommendationsManager.AddTrack(title, artist, duration, mediaUri);
            }
        }

        /// <summary>
        /// Add a queue item to the next place in the playlist after the currently playing track.
        /// </summary>
        /// <param name="args">Command argument <see cref="Bundle"/>.</param>
        private void CommandQueueVideoNext(Bundle args)
        {
            // Check if the queue is empty and call CommandQueueVideoToNewPlaylist if so, then exit
            if (musicQueue.QueueLength == 0)
            {
                CommandQueueVideoToNewPlaylist(args);
                return;
            }

            // Get the command arguments
            var title = args.GetString("title");
            var artist = args.GetString("artist");
            var iconUri = args.GetString("iconUri");
            var mediaUri = args.GetString("mediaUri");
            var duration = args.GetLong("duration", -1);

            if (title != null && artist != null && iconUri != null && mediaUri != null && duration != -1)
            {
                // Build the queue item
                var queueItem = ContentManager.BuildQueueItem(
                    ContentManager.CreateTrackMediaId(ContentManager.DefaultPlaylistName, mediaUri),
                    duration,
                    mediaUri,
                    title,
                    artist,
                    iconUri,
                    musicQueue.QueueLength);

                // Insert the queue item as the next item and set the queue for the session
                musicQueue.InsertNext(queueItem);
                mediaSession.SetQueue(musicQueue.Queue);

                // Add to recommendation data
                RecommendationsManager.AddTrack(title, artist, duration, mediaUri);
            }
        }

        /// <summary>
        /// Add a queue item to the next place in the playlist after the currently playing track.
        /// </summary>
        /// <param name="args">Command argument <see cref="Bundle"/>.</param>
        private void CommandQueueVideoLast(Bundle args)
        {
            // Check if the queue is empty and call CommandQueueVideoToNewPlaylist if so, then exit
            if (musicQueue.QueueLength == 0)
            {
                CommandQueueVideoToNewPlaylist(args);
                return;
            }

            // Get the command arguments
            var title = args.GetString("title");
            var artist = args.GetString("artist");
            var iconUri = args.GetString("iconUri");
            var mediaUri = args.GetString("mediaUri");
            var duration = args.GetLong("duration", -1);
            var playIfStopped = args.GetBoolean("playIfStopped");

            if (title != null && artist != null && iconUri != null && mediaUri != null && duration != -1)
            {
                // Build the queue item
                var queueItem = ContentManager.BuildQueueItem(
                    ContentManager.CreateTrackMediaId(ContentManager.DefaultPlaylistName, mediaUri),
                    duration,
                    mediaUri,
                    title,
                    artist,
                    iconUri,
                    musicQueue.QueueLength);

                // Add the queue item at the end of the list and set the queue for the session
                musicQueue.InsertLast(queueItem);
                mediaSession.SetQueue(musicQueue.Queue);

                // Add to recommendation data
                RecommendationsManager.AddTrack(title, artist, duration, mediaUri);

                // Play the added track if play if stopped flag is set and playback is not currently occuring
                if (playIfStopped && (musicPlayer.PlaybackState == PlaybackStateCode.None || musicPlayer.PlaybackState == PlaybackStateCode.Stopped))
                {
                    musicQueue.SetItemByQueueId(musicQueue.QueueLength - 1);
                    RequestPlay();
                }
            }
        }

        /// <summary>
        /// Saves a video to the end of a previously saved playlist that is not currently loaded to the current queue.
        /// </summary>
        /// <param name="args">Command argument <see cref="Bundle"/>.</param>
        private void CommandSaveVideoToPlaylist(Bundle args)
        {
            // Get the command arguments
            var title = args.GetString("title");
            var artist = args.GetString("artist");
            var iconUri = args.GetString("iconUri");
            var mediaUri = args.GetString("mediaUri");
            var duration = args.GetLong("duration", -1);
            var parentMediaId = args.GetString("parentMediaId");

            if (title != null && artist != null && iconUri != null && mediaUri != null && duration != -1 && parentMediaId != null)
            {
                // Build the media item
                var mediaItem = ContentManager.BuildMediaItem(
                    ContentManager.CreateTrackMediaId(ContentManager.GetPlaylistNameFromMediaId(parentMediaId), mediaUri),
                    duration,
                    mediaUri,
                    title,
                    artist,
                    iconUri);

                // Save the media item to the parent playlist
                ContentManager.SaveMediaItem(parentMediaId, mediaItem);

                // If the parent playlist name matches the currently loaded playlist, add the track to the end of the playlist
                if (mediaSession.Controller.QueueTitle == ContentManager.GetPlaylistNameFromMediaId(parentMediaId))
                {
                    args.PutBoolean("playIfStopped", false);
                    CommandQueueVideoLast(args);
                }
            }
        }

        /// <summary>
        /// Loads a playlist from YouTube into the current queue.
        /// </summary>
        /// <param name="args">Command argument <see cref="Bundle"/>.</param>
        /// <param name="resultReceiver">Callback to receive the result of the command.</param>
        private async void CommandLoadPlaylist(Bundle args, ResultReceiver resultReceiver)
        {
            // Get the command argument
            var playlistId = args.GetString("playlistId");

            if (playlistId != null)
            {
                // Create bundle for command result
                var resultData = new Bundle();
                resultData.PutString("command", Commands.CmdLoadPlaylist);

                // Stop playlist loading if there is no connectivity
                if (!NetworkStatus.IsConnected)
                {
                    // Send canceled command result
                    resultReceiver.Send(Android.App.Result.Canceled, resultData);

                    return;
                }

                // Get cancellation token
                var cancellationToken = PlaylistCancellationToken;

                // Retrieve each video in the playlist
                var queueItems = new List<MediaSession.QueueItem>();
                var playlistVideoIds = new HashSet<string>();
                string playlistTitle = null;
                string continuationToken = null;
                string visitorData = null;
                var processedVideos = 0;
                int initialQueueItemCount;

                try
                {
                    // Loop and continue requesting more pages of items until no continuation token is found
                    do
                    {
                        // Save initial queue item count for reference
                        initialQueueItemCount = queueItems.Count;

                        await foreach (var playlistContentItem in YouTube.GetPlaylistVideos(playlistId, continuationToken, visitorData, cancellationToken))
                        {
                            // Increment processed video count
                            processedVideos++;

                            // Extract playlist title from content item
                            playlistTitle = playlistContentItem.Data["playlistTitle"];

                            // Extract video from content item
                            var video = (YouTube.Video)playlistContentItem.Content;

                            // Process the video if it has not yet been added to the playlist
                            if (!playlistVideoIds.Contains(video.VideoId))
                            {
                                // Add video ID to the hash set
                                playlistVideoIds.Add(video.VideoId);

                                // Save next continuation token if not yet saved or if it has changed
                                if (continuationToken == null || playlistContentItem.ContinuationToken != continuationToken)
                                    continuationToken = playlistContentItem.ContinuationToken;

                                // Save visitor data if not yet saved or if it has changed
                                if (visitorData == null || visitorData != playlistContentItem.VisitorData)
                                    visitorData = playlistContentItem.VisitorData;

                                // Build the queue item
                                var queueItem = ContentManager.ConvertVideoToQueueItem(video, queueItems.Count);

                                // Add the queue item if not already added
                                queueItems.Add(queueItem);
                            }
                        }
                    } // Continue while continuation token is not null and the batch of videos processed was not a multiple of 100 (indicating the final page) or no new videos were added, indicating all duplicates
                    while (!string.IsNullOrEmpty(continuationToken) && (processedVideos % 100 == 0 || queueItems.Count == initialQueueItemCount));
                }
                catch
                {
                    // Send canceled command result
                    resultReceiver.Send(Android.App.Result.Canceled, resultData);

                    return;
                }

                // Set up the queue for the session if items were loaded
                if (queueItems.Count > 0)
                {
                    // Stop any current playback
                    if (musicPlayer.PlaybackState != PlaybackStateCode.None && musicPlayer.PlaybackState != PlaybackStateCode.Stopped)
                        RequestStop();

                    // Reinitialize the music queue and set to session after playlist load
                    musicQueue = new MusicQueue
                    {
                        Queue = queueItems,
                    };

                    mediaSession.SetQueue(musicQueue.Queue);
                    mediaSession.SetQueueTitle(playlistTitle);

                    // Add first track to recommendation data
                    var description = musicQueue.Queue[0].Description;
                    RecommendationsManager.AddTrack(description.Title, description.Subtitle, description.Extras.GetLong("Duration"), description.MediaUri.ToString());

                    // Send ok result
                    resultReceiver.Send(Android.App.Result.Ok, resultData);
                } // Send a canceled result
                else
                {
                    resultReceiver.Send(Android.App.Result.Canceled, resultData);
                }
            }
        }

        /// <summary>
        /// Command to generate a new playlist based on a seed track.
        /// </summary>
        /// <param name="args">Command argument <see cref="Bundle"/>.</param>
        /// <param name="resultReceiver">Callback to receive the result of the command.</param>
        private async void CommandGenerateNewPlaylist(Bundle args, ResultReceiver resultReceiver)
        {
            // Get the video ID from the command arguments
            var videoId = args.GetString("videoId");
            playlistVideoId = videoId;

            // Create bundle for command result
            var resultData = new Bundle();
            resultData.PutString("command", Commands.CmdGenerateNewPlaylist);

            if (playlistVideoId != null)
            {
                // Set queue title to unsaved playlist
                mediaSession.SetQueueTitle(GetString(Resource.String.unsaved_playlist));

                // Stop playlist generation if there is no connectivity
                if (!NetworkStatus.IsConnected)
                {
                    // Send canceled command result
                    resultReceiver.Send(Android.App.Result.Canceled, resultData);

                    return;
                }

                // Get cancellation token
                var cancellationToken = PlaylistCancellationToken;

                // Nullify continuation token, visitor data, and playlist source
                continuationToken = null;
                visitorData = null;

                // Retrieve playlist source preference from settings
                playlistSource = SettingsManager.PlaylistSourceSetting;

                // Initialize queue items list to hold playlist
                var queueItems = new List<MediaSession.QueueItem>();

                var attempts = 0;
                var generationSuccessful = false;

                while (!generationSuccessful && attempts <= 3)
                {
                    // Increment playlist generation attempts
                    attempts++;

                    try
                    {
                        // Search YouTube to get data for the seed video if YouTube is the playlist source, since YouTube does not include the seed video
                        // in the playlist by default
                        if (playlistSource == SettingsManager.PlaylistSourceSettingOptions.YouTube)
                        {
                            // Search video ID from YouTube
                            await foreach (var playlistContentItem in YouTube.SearchYouTube(videoId, null, cancellationToken))
                            {
                                if (playlistContentItem.Content is YouTube.Video video)
                                {
                                    // Build queue item description
                                    var extras = new Bundle();
                                    extras.PutLong("Duration", (long)video.Duration.TotalMilliseconds);
                                    var queueItemDescription = new MediaDescription.Builder()
                                        .SetMediaId(ContentManager.CreateTrackMediaId(ContentManager.DefaultPlaylistName, video.Url))
                                        .SetTitle(video.Title)
                                        .SetSubtitle(video.Author)
                                        .SetIconUri(Android.Net.Uri.Parse(video.Thumbnails.MediumResUrl))
                                        .SetMediaUri(Android.Net.Uri.Parse(video.Url))
                                        .SetExtras(extras)
                                        .Build();

                                    // Build the queue item
                                    queueItems.Add(new MediaSession.QueueItem(queueItemDescription, queueItems.Count));

                                    break;
                                }
                            }
                        }

                        // Retrieve two pages of the watch playlist
                        int pagesRetrieved = 0;

                        do
                        {
                            // Assign task source from YouTube or YouTube Music depending on the source returned from the first round of content
                            IAsyncEnumerable<YouTube.ContentItem> playlistTask;

                            if (playlistSource == SettingsManager.PlaylistSourceSettingOptions.YouTube)
                                playlistTask = YouTube.GetRelatedVideos(playlistVideoId, continuationToken, visitorData, cancellationToken);
                            else
                                playlistTask = YouTube.GetWatchPlaylistVideos(playlistVideoId, continuationToken, visitorData, cancellationToken);

                            await foreach (var playlistContentItem in playlistTask)
                            {
                                // Extract video from content item
                                var video = (YouTube.Video)playlistContentItem.Content;

                                // Save next continuation token if not yet saved or if it has changed
                                if (continuationToken == null || playlistContentItem.ContinuationToken != continuationToken)
                                    continuationToken = playlistContentItem.ContinuationToken;

                                // Save visitor data if not yet saved or if it has changed
                                if (visitorData == null || visitorData != playlistContentItem.VisitorData)
                                    visitorData = playlistContentItem.VisitorData;

                                // Build the queue item
                                var queueItem = ContentManager.ConvertVideoToQueueItem(video, queueItems.Count);

                                // Add the queue item
                                queueItems.Add(queueItem);
                            }

                            // Increment result pages retrieved
                            pagesRetrieved++;

                            // Reset continuation token and visitor data if only one queue item was retrieved, which sometimes happens on the initial YouTube API call
                            // This causes an exit of the inner while loop
                            if (queueItems.Count <= 1)
                            {
                                continuationToken = null;
                                visitorData = null;
                            }
                        }
                        while (!string.IsNullOrEmpty(continuationToken) && pagesRetrieved < GeneratePlaylistPages);

                        // Mark playlist generation as successfull if one or more queue items were retrieved
                        if (queueItems.Count > 1)
                            generationSuccessful = true;
                    }
                    catch
                    {
                        Toast.MakeText(Application.Context, Resource.String.error_generating_playlist, ToastLength.Short).Show();
                    }
                }

                // Set up the queue for the session if generation was successful
                if (generationSuccessful)
                {
                    // Stop any current playback
                    if (musicPlayer.PlaybackState != PlaybackStateCode.None && musicPlayer.PlaybackState != PlaybackStateCode.Stopped)
                        RequestStop();

                    // Shuffle the queue items before adding to the queue and session, keeping the first item in place since this is the seed track
                    MusicQueue.Shuffle(queueItems, true);

                    // Set the queue items
                    musicQueue = new MusicQueue
                    {
                        Queue = queueItems,
                    };

                    // Set queue to media session
                    mediaSession.SetQueue(musicQueue.Queue);

                    // Start playback
                    RequestPlay();

                    // Add first track to recommendation data
                    var description = musicQueue.Queue[0].Description;
                    RecommendationsManager.AddTrack(description.Title, description.Subtitle, description.Extras.GetLong("Duration"), description.MediaUri.ToString());

                    // Send ok result
                    resultReceiver.Send(Android.App.Result.Ok, resultData);

                    return;
                }
            }

            // Send a canceled result if a video ID was not found or no queue items were generated
            resultReceiver.Send(Android.App.Result.Canceled, resultData);
        }

        /// <summary>
        /// Command to generate more tracks for an existing playlist.
        /// </summary>
        /// <param name="resultReceiver">Callback to receive the result of the command.</param>
        private async void CommandContinueGeneratePlaylist(ResultReceiver resultReceiver)
        {
            // Create bundle for command result
            var resultData = new Bundle();
            resultData.PutString("command", Commands.CmdContinueGeneratePlaylist);

            // Check if the continuation token and video ID are populated
            if (!string.IsNullOrEmpty(continuationToken) && !string.IsNullOrEmpty(playlistVideoId))
            {
                // Stop playlist generation if there is no connectivity
                if (!NetworkStatus.IsConnected)
                {
                    // Send canceled command result
                    resultReceiver.Send(Android.App.Result.Canceled, resultData);

                    return;
                }

                // Get cancellation token
                var cancellationToken = PlaylistCancellationToken;

                // Retrieve two pages of the watch playlist
                var mediaItems = new List<MediaBrowser.MediaItem>();
                int pagesRetrieved = 0;

                var attempts = 0;
                var generationSuccessful = false;

                while (!generationSuccessful && attempts <= 3)
                {
                    // Increment playlist generation attempts
                    attempts++;

                    try
                    {
                        do
                        {
                            // Assign task source from YouTube or YouTube Music depending on the source returned from the first round of content
                            IAsyncEnumerable<YouTube.ContentItem> playlistTask;

                            if (playlistSource == SettingsManager.PlaylistSourceSettingOptions.YouTubeMusic)
                                playlistTask = YouTube.GetWatchPlaylistVideos(playlistVideoId, continuationToken, visitorData, cancellationToken);
                            else
                                playlistTask = YouTube.GetRelatedVideos(playlistVideoId, continuationToken, visitorData, cancellationToken);

                            await foreach (var playlistContentItem in playlistTask)
                            {
                                // Extract video from content item
                                var video = (YouTube.Video)playlistContentItem.Content;

                                // Save next continuation token if not yet saved or if it has changed
                                if (continuationToken == null || playlistContentItem.ContinuationToken != continuationToken)
                                    continuationToken = playlistContentItem.ContinuationToken;

                                // Save visitor data if not yet saved or if it has changed
                                if (visitorData == null || visitorData != playlistContentItem.VisitorData)
                                    visitorData = playlistContentItem.VisitorData;

                                // Build the media item
                                var mediaItem = ContentManager.ConvertVideoToMediaItem(video);

                                // Add the media item
                                mediaItems.Add(mediaItem);
                            }

                            // Increment result pages retrieved
                            pagesRetrieved++;
                        }
                        while (!string.IsNullOrEmpty(continuationToken) && pagesRetrieved < GeneratePlaylistPages);

                        // Mark playlist generation as successfull if two or more queue items were retrieved
                        if (mediaItems.Count > 1)
                            generationSuccessful = true;
                    }
                    catch
                    {
                        Toast.MakeText(Application.Context, Resource.String.error_generating_playlist, ToastLength.Short).Show();
                    }
                }

                // Set up the queue for the session if generation was successful
                if (generationSuccessful)
                {
                    // Convert to queue items by adding a queue ID, shuffling new items, and adding to the queue
                    var queueItems = ContentManager.ConvertMediaItemsToQueue(mediaItems, musicQueue.QueueLength);
                    MusicQueue.Shuffle(queueItems, false);

                    foreach (var queueItem in queueItems)
                    {
                        musicQueue.InsertLast(queueItem);
                    }

                    // Set queue to media session
                    mediaSession.SetQueue(musicQueue.Queue);

                    // Send ok result
                    resultReceiver.Send(Android.App.Result.Ok, resultData);

                    return;
                }
            }

            // Send canceled result if no continuation token, video ID, or queue items were found
            resultReceiver.Send(Android.App.Result.Canceled, resultData);
        }

        /// <summary>
        /// Generates a playlist based on user recommendations.
        /// </summary>
        /// <param name="resultReceiver">Callback to receive the result of the command.</param>
        private async void CommandGenerateRecommendedPlaylist(ResultReceiver resultReceiver)
        {
            // Create bundle for command result
            var resultData = new Bundle();
            resultData.PutString("command", Commands.CmdContinueGeneratePlaylist);

            // Stop playlist generation if there is no connectivity
            if (!NetworkStatus.IsConnected)
            {
                // Send canceled command result
                resultReceiver.Send(Android.App.Result.Canceled, resultData);

                return;
            }

            // Get cancellation token
            var cancellationToken = PlaylistCancellationToken;

            // Set queue title
            mediaSession.SetQueueTitle(Application.Context.GetString(Resource.String.recommended_mix));

            // Initialize seed track data
            seedTrackData = new List<RecommendationsManager.SeedTrackData>();

            // Initialize queue items list to hold playlist
            var queueItems = new List<MediaSession.QueueItem>();

            // Get playlist tracks based on recommendations
            List<YouTube.ContentItem> playlistContentItems = new List<YouTube.ContentItem>();

            var attempts = 0;
            var generationSuccessful = false;

            while (!generationSuccessful && attempts <= 3)
            {
                // Increment playlist generation attempts
                attempts++;

                // Generate a playlist based on recommendations
                try
                {
                    var recommendedPlaylistData = await RecommendationsManager.GenerateRecommendedPlaylist(seedTrackData, cancellationToken);
                    playlistContentItems = recommendedPlaylistData.Item1;
                    seedTrackData = recommendedPlaylistData.Item2;
                }
                catch
                {
                    Toast.MakeText(Application.Context, Resource.String.error_generating_playlist, ToastLength.Short).Show();
                }

                // Mark playlist generation as successfull if one or more queue items were retrieved
                if (playlistContentItems.Count > 0)
                    generationSuccessful = true;
            }

            // Convert to queue item
            foreach (var playlistContentItem in playlistContentItems)
            {
                var video = (YouTube.Video)playlistContentItem.Content;

                // Build the queue item
                var queueItem = ContentManager.ConvertVideoToQueueItem(video, queueItems.Count);

                // Add the queue item
                queueItems.Add(queueItem);
            }

            // Set up the queue for the session if generation was successful
            if (generationSuccessful)
            {
                // Stop any current playback
                if (musicPlayer.PlaybackState != PlaybackStateCode.None && musicPlayer.PlaybackState != PlaybackStateCode.Stopped)
                    RequestStop();

                // Shuffle the queue items before adding to the queue and session, keeping the first item in place since this is the seed track
                MusicQueue.Shuffle(queueItems, true);

                // Set the queue items
                musicQueue = new MusicQueue
                {
                    Queue = queueItems,
                };

                // Set queue and title to media session
                mediaSession.SetQueue(musicQueue.Queue);

                // Start playback
                RequestPlay();

                // Send ok result
                resultReceiver.Send(Android.App.Result.Ok, resultData);
            }
            else
            {
                // Send canceled result
                resultReceiver.Send(Android.App.Result.Canceled, resultData);
            }
        }

        /// <summary>
        /// Command to generate more tracks for a recommendation playlist.
        /// </summary>
        /// <param name="resultReceiver">Callback to receive the result of the command.</param>
        private async void CommandContinueGenerateRecommendedPlaylist(ResultReceiver resultReceiver)
        {
            // Create bundle for command result
            var resultData = new Bundle();
            resultData.PutString("command", Commands.CmdContinueGeneratePlaylist);

            // Check that seed track data is available
            if (seedTrackData.Count > 0)
            {
                // Stop playlist generation if there is no connectivity
                if (!NetworkStatus.IsConnected)
                {
                    // Send canceled command result
                    resultReceiver.Send(Android.App.Result.Canceled, resultData);

                    return;
                }

                // Get cancellation token
                var cancellationToken = PlaylistCancellationToken;

                // Initialize queue items list to hold playlist
                var mediaItems = new List<MediaBrowser.MediaItem>();

                // Get playlist tracks based on recommendations
                List<YouTube.ContentItem> playlistContentItems = new List<YouTube.ContentItem>();

                var attempts = 0;
                var generationSuccessful = false;

                while (!generationSuccessful && attempts <= 3)
                {
                    // Increment playlist generation attempts
                    attempts++;

                    // Generate a playlist based on recommendations
                    try
                    {
                        var recommendedPlaylistData = await RecommendationsManager.GenerateRecommendedPlaylist(seedTrackData, cancellationToken);
                        playlistContentItems = recommendedPlaylistData.Item1;
                        seedTrackData = recommendedPlaylistData.Item2;
                    }
                    catch
                    {
                        Toast.MakeText(Application.Context, Resource.String.error_generating_playlist, ToastLength.Short).Show();
                    }

                    // Mark playlist generation as successfull if one or more queue items were retrieved
                    if (playlistContentItems.Count > 0)
                        generationSuccessful = true;
                }

                // Convert to queue item
                foreach (var playlistContentItem in playlistContentItems)
                {
                    var video = (YouTube.Video)playlistContentItem.Content;

                    // Build the media item
                    var mediaItem = ContentManager.ConvertVideoToMediaItem(video);

                    // Add the media item
                    mediaItems.Add(mediaItem);
                }

                // Set up the queue for the session if generation was successful
                if (generationSuccessful)
                {
                    // Convert to queue items by adding a queue ID, shuffling new items, and adding to the queue
                    var queueItems = ContentManager.ConvertMediaItemsToQueue(mediaItems, musicQueue.QueueLength);
                    MusicQueue.Shuffle(queueItems, false);

                    foreach (var queueItem in queueItems)
                    {
                        musicQueue.InsertLast(queueItem);
                    }

                    // Set queue to media session
                    mediaSession.SetQueue(musicQueue.Queue);

                    // Send ok result
                    resultReceiver.Send(Android.App.Result.Ok, resultData);

                    return;
                }
            }

            // Send canceled command result if there is no existing seed track data or if no playlist items were generated
            resultReceiver.Send(Android.App.Result.Canceled, resultData);
        }

        /// <summary>
        /// Generates a playlist consisting of tracks the user has previously listened to.
        /// </summary>
        /// <param name="resultReceiver">Callback to receive the result of the command.</param>
        private void CommandGenerateHistoricalPlaylist(ResultReceiver resultReceiver)
        {
            // Create bundle and send the command result
            var resultData = new Bundle();
            resultData.PutString("command", Commands.CmdContinueGeneratePlaylist);

            // Set queue title
            mediaSession.SetQueueTitle(Application.Context.GetString(Resource.String.listen_again_mix));

            // Convert historical tracks into queue items
            var queueItems = new List<MediaSession.QueueItem>();

            foreach (var video in RecommendationsManager.HistoricalTracks)
            {
                // Build the queue item
                var queueItem = ContentManager.ConvertVideoToQueueItem(video, queueItems.Count);

                // Add the queue item
                queueItems.Add(queueItem);
            }

            // Send a canceled result if no queue items were found
            if (queueItems.Count == 0)
            {
                resultReceiver.Send(Android.App.Result.Canceled, resultData);

                return;
            }

            // Stop any current playback
            if (musicPlayer.PlaybackState != PlaybackStateCode.None && musicPlayer.PlaybackState != PlaybackStateCode.Stopped)
                RequestStop();

            // Shuffle the queue items before adding to the queue and session, keeping the first item in place since this is the seed track
            MusicQueue.Shuffle(queueItems, true);

            // Set the queue items
            musicQueue = new MusicQueue
            {
                Queue = queueItems,
            };

            // Set queue to media session
            mediaSession.SetQueue(musicQueue.Queue);

            // Start playback
            RequestPlay();

            // Send ok result
            resultReceiver.Send(Android.App.Result.Ok, resultData);
        }

        /// <summary>
        /// Holds available custom commands for the media session.
        /// </summary>
        public static class Commands
        {
            /// <summary>
            /// Command to enable shuffle mode.
            /// Arguments: None.
            /// </summary>
            public const string CmdEnableShuffleMode = "CMD_ENABLE_SHUFFLE_MODE";

            /// <summary>
            /// Command to disable shuffle mode.
            /// Arguments: None.
            /// </summary>
            public const string CmdDisableShuffleMode = "CMD_DISABLE_SHUFFLE_MODE";

            /// <summary>
            /// Command to save the current queue items to the content file.
            /// Arguments: parentMediaId (string) - The media ID of the parent playlist to save the queue items with.
            /// </summary>
            public const string CmdSaveQueueItems = "CMD_SAVE_QUEUE_ITEMS";

            /// <summary>
            /// Command to delete a playlist from the content file by parent media ID, if the playlist exists.
            /// Arguments: parentMediaId (string) - The media ID of the parent playlist to delete.
            /// </summary>
            public const string CmdDeleteMediaItems = "CMD_DELETE_MEDIA_ITEMS";

            /// <summary>
            /// Command to stop playback and clear all items from the current queue.
            /// Arguments: None.
            /// </summary>
            public const string CmdClearQueueItems = "CMD_CLEAR_QUEUE_ITEMS";

            /// <summary>
            /// Command to move a queue item from to a specified position using its media ID.
            /// Arguments: mediaId (string) - The media ID of the item to move, toPosition (int) - The integer queue position to move the item to.
            /// </summary>
            public const string CmdMoveQueueItem = "CMD_MOVE_QUEUE_ITEM";

            /// <summary>
            /// Command to move a queue item from to a specified position using its media ID.
            /// Arguments: position (int) - The integer queue position to remove the item from.
            /// </summary>
            public const string CmdRemoveQueueItem = "CMD_REMOVE_QUEUE_ITEM";

            /// <summary>
            /// Command to create a new queue item from a video and add it as a single item in a new queue.
            /// Arguments: title (string) - The title of the video, artist (string) - The channel name of the video, iconUri (Uri) - Uri of the video thumbnail, mediaUri (Uri) - Uri of the media source,
            /// duration (int) - Duration in milliseconds of the video.
            /// </summary>
            public const string CmdQueueVideoToNewPlaylist = "CMD_QUEUE_VIDEO_TO_NEW_PLAYLIST";

            /// <summary>
            /// Command to add a video as the next item to play after the current queue item.
            /// Arguments: title (string) - The title of the video, artist (string) - The channel name of the video, iconUri (Uri) - Uri of the video thumbnail, mediaUri (Uri) - Uri of the media source,
            /// duration (int) - Duration in milliseconds of the video.
            /// </summary>
            public const string CmdQueueVideoNext = "CMD_QUEUE_VIDEO_NEXT";

            /// <summary>
            /// Command to create a new queue item from a video and add it as the last upcoming queue item in the current queue. If no current queue exists, a new queue is created.
            /// Arguments: title (string) - The title of the video, artist (string) - The channel name of the video, iconUri (Uri) - Uri of the video thumbnail, mediaUri (Uri) - Uri of the media source,
            /// duration (int) - Duration in milliseconds of the video, playIfStopped (bool) - If true, plays the added track if no playback is currently occurring.
            /// </summary>
            public const string CmdQueueVideoLast = "CMD_QUEUE_VIDEO_LAST";

            /// <summary>
            /// Command to save a video to the end of a previously saved playlist that is not currently loaded to the current queue.
            /// Arguments: title (string) - The title of the video, artist (string) - The channel name of the video, iconUri (Uri) - Uri of the video thumbnail, mediaUri (Uri) - Uri of the media source,
            /// duration (int) - Duration in milliseconds of the video, parentMediaId (string) - The media ID of the parent playlist to save the video to.
            /// </summary>
            public const string CmdSaveVideoToPlaylist = "CMD_SAVE_VIDEO_TO_PLAYLIST";

            /// <summary>
            /// Command to load a playlist from YouTube using a playlist ID.
            /// Arguments: playlistId (string) - The playlist ID of the playlist to load.
            /// </summary>
            public const string CmdLoadPlaylist = "CMD_LOAD_PLAYLIST";

            /// <summary>
            /// Command to generate a new playlist based on a seed track.
            /// Arguments: videoId (string) - The video ID of the seed track for the playlist.
            /// </summary>
            public const string CmdGenerateNewPlaylist = "CMD_GENERATE_NEW_PLAYLIST";

            /// <summary>
            /// Command to generate more tracks for an existing playlist.
            /// </summary>
            public const string CmdContinueGeneratePlaylist = "CMD_CONTINUE_GENERATE_PLAYLIST";

            /// <summary>
            /// Command to generate a new playlist based on tracks the user has previously listened to.
            /// </summary>
            public const string CmdGenerateRecommendedPlaylist = "CMD_GENERATE_RECOMMENDED_PLAYLIST";

            /// <summary>
            /// Command to generate more tracks for the recommended playlist.
            /// </summary>
            public const string CmdContinueGenerateRecommendedPlaylist = "CMD_CONTINUE_GENERATE_RECOMMENDED_PLAYLIST";

            /// <summary>
            /// Command to generate a playlist consisting of tracks the user has previously listened to.
            /// </summary>
            public const string CmdGenerateHistoricalPlaylist = "CMD_GENERATE_HISTORICAL_PLAYLIST";
        }

        /// <summary>
        /// Holds custom metadata keys to store additional metadata.
        /// </summary>
        public static class MetadataCustomKeys
        {
            /// <summary>
            /// Key to store a backup album art URI if the original URI is not valid.
            /// </summary>
            public const string AlbumArtUriBackup = "ALBUM_ART_URI_BACKUP";

            /// <summary>
            /// Key to store color data to generate a dynamic gradient theme on the full screen player.
            /// </summary>
            public const string GradientData = "GRADIENT_DATA";
        }

        /// <summary>
        /// Callbacks for the <see cref="MediaSession"/>.
        /// </summary>
        public class MediaSessionCallback : MediaSession.Callback
        {
            public Action OnPauseAction { get; set; }

            public Action OnPlayAction { get; set; }

            public Action OnStopAction { get; set; }

            public Action<string, Bundle> OnPlayFromMediaIdAction { get; set; }

            public Action<long> OnSeekToAction { get; set; }

            public Action OnSkipToNextAction { get; set; }

            public Action OnSkipToPreviousAction { get; set; }

            public Action<long> OnSkipToQueueItemAction { get; set; }

            public Action<string, Bundle, ResultReceiver> OnCommandAction { get; set; }

            public override void OnPause()
            {
                OnPauseAction();
            }

            public override void OnPlay()
            {
                OnPlayAction();
            }

            public override void OnStop()
            {
                OnStopAction();
            }

            public override void OnPlayFromMediaId(string mediaId, Bundle extras)
            {
                OnPlayFromMediaIdAction(mediaId, extras);
            }

            public override void OnSeekTo(long pos)
            {
                OnSeekToAction(pos);
            }

            public override void OnSkipToNext()
            {
                OnSkipToNextAction();
            }

            public override void OnSkipToPrevious()
            {
                OnSkipToPreviousAction();
            }

            public override void OnSkipToQueueItem(long queueId)
            {
                OnSkipToQueueItemAction(queueId);
            }

            public override void OnCommand(string command, Bundle args, ResultReceiver cb)
            {
                OnCommandAction(command, args, cb);
            }
        }

        /// <summary>
        /// Class to hold queue item data that must be loaded with asynchronous tasks.
        /// </summary>
        public class QueueItemAsyncData
        {
            private readonly CancellationToken cancellationToken;

            /// <summary>
            /// Initializes a new instance of the <see cref="QueueItemAsyncData"/> class.
            /// </summary>
            /// <param name="queueItem">The <see cref="MediaSession.QueueItem"/> to load asynchronous data for.</param>
            /// <param name="cancellationToken">The cancellation token to interrupt tasks if network connectivity is lost.</param>
            public QueueItemAsyncData(MediaSession.QueueItem queueItem, CancellationToken cancellationToken)
            {
                QueueItem = queueItem ?? throw new ArgumentNullException("The queueItem argument can not be null.");
                this.cancellationToken = cancellationToken;

                LoadData();
            }

            public MediaSession.QueueItem QueueItem { get; private set; }

            public Task<string> StreamUrl { get; private set; }

            public Task<MediaMetadata> MediaMetadata { get; private set; }

            /// <summary>
            /// Loads asynchronous data for the queue item.
            /// </summary>
            private void LoadData()
            {
                // Run tasks if network is available
                if (NetworkStatus.IsConnected)
                {
                    // Get the stream URL loading task
                    StreamUrl = LoadStreamUrl();

                    // Get the media metadata loading task
                    MediaMetadata = LoadMediaMetadata();
                }
            }

            /// <summary>
            /// Loads the stream URL for the queue item.
            /// </summary>
            /// <returns>A <see cref="Task"/> containing the URL.</returns>
            private async Task<string> LoadStreamUrl()
            {
                return await MusicProvider.GetMusicStream(QueueItem.Description.MediaUri.ToString(), SettingsManager.StreamQualitySetting, cancellationToken);
            }

            /// <summary>
            /// Loads media metadata for the queue item.
            /// </summary>
            /// <returns>A <see cref="Task"/> containing the <see cref="MediaSession.MediaMetadata"/>.</returns>
            private async Task<MediaMetadata> LoadMediaMetadata()
            {
                var mediaMetadata = ContentManager.GetQueueItemMetadata(QueueItem);

                if (mediaMetadata != null)
                {
                    // Add album art and icon bitmaps to metadata
                    var iconUrl = mediaMetadata.GetString(Android.Media.MediaMetadata.MetadataKeyDisplayIconUri);
                    var albumArtUrl = mediaMetadata.GetString(Android.Media.MediaMetadata.MetadataKeyAlbumArtUri);
                    var albumArtUrlBackup = mediaMetadata.GetString(MusicService.MetadataCustomKeys.AlbumArtUriBackup);

                    // Set up icon and album art image download tasks
                    var iconTask = ImageService.Instance.LoadUrl(iconUrl).AsBitmapDrawableAsync();
                    var albumArtTask = ImageService.Instance.LoadUrl(albumArtUrl).AsBitmapDrawableAsync();

                    FFImageLoading.Drawables.SelfDisposingBitmapDrawable icon;
                    FFImageLoading.Drawables.SelfDisposingBitmapDrawable albumArt;
                    var downloadAttempts = 1;

                    while (downloadAttempts <= 3)
                    {
                        // Attempt to download icon and album art
                        try
                        {
                            icon = await iconTask.WaitOrCancel(cancellationToken);

                            try
                            {
                                albumArt = await albumArtTask.WaitOrCancel(cancellationToken);
                            }
                            catch (FFImageLoading.Exceptions.DownloadHttpStatusCodeException)
                            {
                                albumArtTask = ImageService.Instance.LoadUrl(albumArtUrlBackup).AsBitmapDrawableAsync();
                                albumArt = await albumArtTask.WaitOrCancel(cancellationToken);
                            }

                            // Get colors from album art
                            int bottomColor, topColor;

                            var palette = Palette.From(albumArt.Bitmap).Generate();

                            try
                            {
                                if (palette.VibrantSwatch != null)
                                    bottomColor = palette.VibrantSwatch.Rgb;
                                else
                                    bottomColor = palette.Swatches[0].Rgb;

                                if (palette.DarkMutedSwatch != null)
                                    topColor = palette.DarkMutedSwatch.Rgb;
                                else
                                    topColor = palette.Swatches[1].Rgb;
                            }
                            catch
                            {
                                // Assign the default background color if there is an issue extracting swatches
                                var themeManager = new ThemeManager(Application.Context);
                                bottomColor = themeManager.ColorBackground;
                                topColor = themeManager.ColorBackground;
                            }

                            // Choose a random gradient orientation
                            string orientation = (double)new Random().NextDouble() switch
                            {
                                double number when number >= 0 && number <= 0.5 => "TrBl",
                                double number when number > 0.5 => "TlBr",
                                _ => "TlBr",
                            };

                            var gradientData = $"{bottomColor},{topColor},{orientation}";

                            // Build metadata and return
                            mediaMetadata = new MediaMetadata.Builder(mediaMetadata)
                                .PutBitmap(Android.Media.MediaMetadata.MetadataKeyDisplayIcon, icon.Bitmap)
                                .PutBitmap(Android.Media.MediaMetadata.MetadataKeyAlbumArt, albumArt.Bitmap)
                                .PutString(MetadataCustomKeys.GradientData, gradientData)
                                .Build();

                            return mediaMetadata;
                        } // Cancel all loading tasks if connectivity drops and throw the operation canceled exception
                        catch (System.OperationCanceledException exception)
                        {
                            ImageService.Instance.SetPauseWorkAndCancelExisting(true);
                            ImageService.Instance.SetPauseWorkAndCancelExisting(false);

                            // Throw the exception after stopping work so it can be caught by calling methods
                            throw exception;
                        } // Handle null reference exceptions that can occur when an image download fails
                        catch (NullReferenceException)
                        {
                            // Trigger short delay and increment the attempt counter
                            await Task.Delay(100);
                            downloadAttempts++;
                        }
                    }
                }

                // Return null if either meta data is initially null, or download was not successful
                return null;
            }
        }

        /// <summary>
        /// Stops the <see cref="MusicService"/> if playback is not occuring.
        /// </summary>
        private class DelayedStopHandler : Handler
        {
            private readonly WeakReference<MusicService> weakReference;

            /// <summary>
            /// Initializes a new instance of the <see cref="DelayedStopHandler"/> class.
            /// </summary>
            /// <param name="looper">Main <see cref="Looper"/> to run the message loop.</param>
            /// <param name="musicService">Weak reference to the <see cref="MusicService"/>.</param>
            public DelayedStopHandler(Looper looper, MusicService musicService)
                : base(looper)
            {
                weakReference = new WeakReference<MusicService>(musicService);
            }

            /// <summary>
            /// Stops the <see cref="MusicService"/>.
            /// </summary>
            /// <param name="msg">Input message.</param>
            public override void HandleMessage(Message msg)
            {
                weakReference.TryGetTarget(out MusicService musicService);

                if (musicService != null && musicService.musicPlayer != null)
                {
                    if (musicService.musicPlayer.IsPlaying)
                    {
                        return;
                    }
                    else
                    {
                        musicService.StopSelf();
                        musicService.serviceRunning = false;
                    }
                }
            }
        }
    }
}