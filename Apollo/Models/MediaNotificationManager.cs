// <copyright file="MediaNotificationManager.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.OS;

namespace Apollo
{
    /// <summary>
    /// Manages the media notification.
    /// </summary>
    internal class MediaNotificationManager : BroadcastReceiver
    {
        public const string ActionPause = "com.erikb.Apollo.mediabrowserservice.pause";
        public const string ActionPlay = "com.erikb.Apollo.mediabrowserservice.play";
        public const string ActionStop = "com.erikb.Apollo.mediabrowserservice.stop";
        public const string ActionPrevious = "com.erikb.Apollo.mediabrowserservice.prev";
        public const string ActionNext = "com.erikb.Apollo.mediabrowserservice.next";
        private const int NotificationId = 666;
        private const int RequestCode = 100;
        private const string ChannelId = "234987";
        private readonly NotificationManager notificationManager;
        private readonly PendingIntent pauseIntent;
        private readonly PendingIntent playIntent;
        private readonly PendingIntent stopIntent;
        private readonly PendingIntent skipToPreviousIntent;
        private readonly PendingIntent skipToNextIntent;
        private readonly MusicService musicService;
        private readonly MediaCallback mediaCallback;
        private MediaSession.Token sessionToken;
        private MediaController mediaController;
        private MediaController.TransportControls transportControls;
        private bool started;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaNotificationManager"/> class.
        /// </summary>
        /// <param name="service">An instance of the associated <see cref="MusicService"/>.</param>
        public MediaNotificationManager(MusicService service)
        {
            musicService = service;
            mediaCallback = new MediaCallback();
            UpdateSessionToken();

            // Set up notification manager
            notificationManager = (NotificationManager)musicService.GetSystemService(Context.NotificationService);

            // Set up notification channel for API 26+
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channelName = musicService.GetString(Resource.String.notification_channel_name);
                var channelDescription = musicService.GetString(Resource.String.notification_channel_des);
                var channelImportance = NotificationImportance.Default;

                // Create new channel
                var notificationChannel = new NotificationChannel(ChannelId, channelName, channelImportance)
                {
                    Description = channelDescription,
                    Importance = NotificationImportance.Low,
                };

                notificationManager.CreateNotificationChannel(notificationChannel);
            }

            // Set up control intents
            var package = musicService.PackageName;
            pauseIntent = PendingIntent.GetBroadcast(musicService, RequestCode, new Intent(ActionPause).SetPackage(package), PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable);
            playIntent = PendingIntent.GetBroadcast(musicService, RequestCode, new Intent(ActionPlay).SetPackage(package), PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable);
            stopIntent = PendingIntent.GetBroadcast(musicService, RequestCode, new Intent(ActionStop).SetPackage(package), PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable);
            skipToPreviousIntent = PendingIntent.GetBroadcast(musicService, RequestCode, new Intent(ActionPrevious).SetPackage(package), PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable);
            skipToNextIntent = PendingIntent.GetBroadcast(musicService, RequestCode, new Intent(ActionNext).SetPackage(package), PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable);

            // Clear all previous notifications
            notificationManager.CancelAll();

            mediaCallback.OnPlaybackStateChangedAction = (playbackState) =>
            {
                // Stop the notification on a null or stopped playback state
                if (playbackState != null && playbackState.State == PlaybackStateCode.Stopped)
                {
                    StopNotification();
                } // Create and start the notification for all other cases
                else
                {
                    Notification notification = CreateNotification();

                    if (notification != null)
                        notificationManager.Notify(NotificationId, notification);
                }
            };

            mediaCallback.OnMetadataChangedAction = (metadata) =>
            {
                // Update the notification when the metadata changes
                Notification notification = CreateNotification();

                if (notification != null)
                    notificationManager.Notify(NotificationId, notification);
            };

            mediaCallback.OnSessionDestroyedAction = () =>
            {
                // Reset the session token when the current session is destroyed
                UpdateSessionToken();
            };
        }

        /// <summary>
        /// Creates a <see cref="Notification"/> object for the app.
        /// </summary>
        /// <returns>A set up <see cref="Notification"/>.</returns>
        public Notification CreateNotification()
        {
            var playbackState = mediaController.PlaybackState;
            var mediaMetadata = mediaController.Metadata;

            if (mediaMetadata == null || playbackState == null)
                return null;

            // Construct builder with context and channel ID for API 26+
            Notification.Builder notificationBuilder;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                notificationBuilder = new Notification.Builder(Application.Context, ChannelId);
            } // Construct with music service for older API's
            else
            {
                #pragma warning disable CS0618 // Type or member is obsolete
                notificationBuilder = new Notification.Builder(musicService);
                #pragma warning restore CS0618 // Type or member is obsolete
            }

            // Add skip previous action
            notificationBuilder.AddAction(new Notification.Action.Builder(
                    Resource.Drawable.ic_skip_previous_control,
                    musicService.GetString(Resource.String.label_skip_prev),
                    skipToPreviousIntent)
                    .Build());

            // Add stop action
            notificationBuilder.AddAction(new Notification.Action.Builder(
                    Resource.Drawable.ic_stop_control,
                    musicService.GetString(Resource.String.label_stop),
                    stopIntent)
                    .Build());

            // Add the pause action if the playback state is currently playing or buffering
            if (playbackState.State == PlaybackStateCode.Playing || playbackState.State == PlaybackStateCode.Buffering)
            {
                notificationBuilder.AddAction(new Notification.Action.Builder(
                    Resource.Drawable.ic_pause_control,
                    musicService.GetString(Resource.String.label_pause),
                    pauseIntent)
                    .Build());
            } // Add the play action for any other playback state
            else
            {
                notificationBuilder.AddAction(new Notification.Action.Builder(
                    Resource.Drawable.ic_play_control,
                    musicService.GetString(Resource.String.label_play),
                    playIntent)
                    .Build());
            }

            // Add skip next action
            notificationBuilder.AddAction(new Notification.Action.Builder(
                    Resource.Drawable.ic_skip_next_control,
                    musicService.GetString(Resource.String.label_skip_next),
                    skipToNextIntent)
                    .Build());

            // Get album art from metadata
            var mediaDescription = mediaMetadata.Description;
            string albumArtUrl;
            Bitmap albumArt = null;

            if (mediaDescription.IconUri != null)
            {
                albumArtUrl = mediaDescription.IconUri.ToString();
                albumArt = new BitmapLoader().LoadBitmapFromUrl(albumArtUrl);
            }

            // Build the notification
            notificationBuilder
                .SetStyle(new Notification.MediaStyle()
                    .SetShowActionsInCompactView(new[] { 0, 2, 3 })
                    .SetMediaSession(sessionToken))
                .SetSmallIcon(Resource.Drawable.ic_play_control_default)
                .SetVisibility(NotificationVisibility.Public)
                .SetUsesChronometer(true)
                .SetContentIntent(CreateContentIntent())
                .SetContentTitle(mediaDescription.Title)
                .SetContentText(mediaDescription.Subtitle)
                .SetLargeIcon(albumArt);

            SetNotificationPlaybackState(notificationBuilder);

            return notificationBuilder.Build();
        }

        /// <summary>
        /// Starts showing the notification.
        /// </summary>
        public void StartNotification()
        {
            // Continue if the notification is not yet started
            if (!started)
            {
                Notification notification = CreateNotification();

                // Continue with the starting process if the notification was successfully created
                if (notification != null)
                {
                    mediaController.RegisterCallback(mediaCallback);

                    var filter = new IntentFilter();
                    filter.AddAction(ActionStop);
                    filter.AddAction(ActionPlay);
                    filter.AddAction(ActionPause);
                    filter.AddAction(ActionNext);
                    filter.AddAction(ActionPrevious);
                    musicService.RegisterReceiver(this, filter);

                    musicService.StartForeground(NotificationId, notification);
                    started = true;
                }
            }
        }

        /// <summary>
        /// Stops showing the notification.
        /// </summary>
        public void StopNotification()
        {
            // Continue if the process is started
            if (started)
            {
                started = false;
                mediaController.UnregisterCallback(mediaCallback);

                try
                {
                    notificationManager.Cancel(NotificationId);
                    musicService.UnregisterReceiver(this);
                }
                catch (ArgumentException)
                {
                    // Ignore if the receiver is not registered
                }

                musicService.StopForeground(true);
            }
        }

        /// <summary>
        /// Maps the received intent action with the proper transport control function.
        /// </summary>
        /// <param name="context">App <see cref="Context"/>.</param>
        /// <param name="intent">The <see cref="Intent"/> that was received.</param>
        public override void OnReceive(Context context, Intent intent)
        {
            // Map the correct transport control action to the received intent
            switch (intent.Action)
            {
                case ActionStop:
                    transportControls.Stop();
                    break;
                case ActionPause:
                    transportControls.Pause();
                    break;
                case ActionPlay:
                    transportControls.Play();
                    break;
                case ActionNext:
                    transportControls.SkipToNext();
                    break;
                case ActionPrevious:
                    transportControls.SkipToPrevious();
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Creates an intent to open the main app UI if the notification is clicked.
        /// </summary>
        /// <returns><see cref="PendingIntent"/> to open the main UI.</returns>
        private PendingIntent CreateContentIntent()
        {
            var openUiIntent = new Intent(musicService, typeof(MainActivity));
            openUiIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ResetTaskIfNeeded);

            return PendingIntent.GetActivity(musicService, RequestCode, openUiIntent, PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable);
        }

        /// <summary>
        /// Updates the transport controls to the latest mediua session token if the token is no longer valid.
        /// </summary>
        private void UpdateSessionToken()
        {
            // Get the latest token from the music service
            var newToken = musicService.SessionToken;

            // Continue if the current session token is null or does not equal the new token
            if (sessionToken == null || sessionToken != newToken)
            {
                // Unregister callback
                if (mediaController != null)
                    mediaController.UnregisterCallback(mediaCallback);

                // Update to the new token
                sessionToken = newToken;
                mediaController = new MediaController(musicService, sessionToken);
                transportControls = mediaController.GetTransportControls();

                // Reregister callback
                if (started)
                    mediaController.RegisterCallback(mediaCallback);
            }
        }

        /// <summary>
        /// Modifies the appearance or behavior of the notification to match the current <see cref="PlaybackState"/>.
        /// </summary>
        /// <param name="notificationBuilder">The <see cref="Notification.Builder"/> to modify.</param>
        private void SetNotificationPlaybackState(Notification.Builder notificationBuilder)
        {
            var playbackState = mediaController.PlaybackState;

            // Stop notification if the playback state is null or the notification is not started
            if (playbackState == null || !started)
            {
                musicService.StopForeground(true);
                return;
            } // Show the current track times if the playback state is playing or in a playback position at 0 or greater
            else if (playbackState.State == PlaybackStateCode.Playing && playbackState.Position >= 0)
            {
                var unixEpochStartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var timespan = ((long)(DateTime.UtcNow - unixEpochStartTime).TotalMilliseconds) - playbackState.Position;

                notificationBuilder
                    .SetWhen(timespan)
                    .SetShowWhen(true);
            } // Do not show track times in any other playback state
            else
            {
                notificationBuilder
                    .SetWhen(0)
                    .SetShowWhen(false);
            }

            // Allow the notification to be dismissed by the user when not playing
            notificationBuilder.SetOngoing(playbackState.State == PlaybackStateCode.Playing);
        }

        /// <summary>
        /// Receives callbacks from the media sesssion.
        /// </summary>
        private class MediaCallback : MediaController.Callback
        {
            public Action<PlaybackState> OnPlaybackStateChangedAction { get; set; }

            public Action<MediaMetadata> OnMetadataChangedAction { get; set; }

            public Action OnSessionDestroyedAction { get; set; }

            public override void OnPlaybackStateChanged(PlaybackState state)
            {
                OnPlaybackStateChangedAction(state);
            }

            public override void OnMetadataChanged(MediaMetadata metadata)
            {
                OnMetadataChangedAction(metadata);
            }

            public override void OnSessionDestroyed()
            {
                base.OnSessionDestroyed();
                OnSessionDestroyedAction();
            }
        }
    }
}