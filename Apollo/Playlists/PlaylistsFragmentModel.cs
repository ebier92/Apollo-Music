// <copyright file="PlaylistsFragmentModel.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

namespace Apollo
{
    /// <summary>
    /// Model class for the <see cref="PlaylistsFragmentPresenter"/>.
    /// </summary>
    internal class PlaylistsFragmentModel
    {
        private readonly MusicBrowser musicBrowser;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistsFragmentModel"/> class.
        /// </summary>
        /// <param name="musicBrowser">A connected instance of a <see cref="MusicBrowser"/>.</param>
        public PlaylistsFragmentModel(MusicBrowser musicBrowser)
        {
            this.musicBrowser = musicBrowser;
        }

        /// <summary>
        /// Subscribes the <see cref="MusicBrowser"/> to a media ID and registers a <see cref="MusicBrowser.SubscriptionCallback"/> to listen for the results.
        /// </summary>
        /// <param name="mediaId">Playlist media ID.</param>
        /// <param name="subscriptionCallback"><see cref="MusicBrowser.SubscriptionCallback"/> class to listen for the results of the subscription.</param>
        public void SubscribeMusicBrowser(string mediaId, MusicBrowser.SubscriptionCallback subscriptionCallback)
        {
            if (musicBrowser != null && musicBrowser.IsConnected)
            {
                musicBrowser.Subscribe(mediaId, subscriptionCallback);
            }
        }

        /// <summary>
        /// Unsubscribes the <see cref="MusicBrowser"/> to a media ID and unregisters a <see cref="MusicBrowser.SubscriptionCallback"/> to listen for the results.
        /// </summary>
        /// <param name="mediaId">Content media ID.</param>
        /// <param name="subscriptionCallback"><see cref="MusicBrowser.SubscriptionCallback"/> class to unregister.</param>
        public void UnsubscribeMusicBrowser(string mediaId, MusicBrowser.SubscriptionCallback subscriptionCallback)
        {
            if (musicBrowser != null && musicBrowser.IsConnected)
            {
                musicBrowser.Unsubscribe(mediaId, subscriptionCallback);
            }
        }
    }
}