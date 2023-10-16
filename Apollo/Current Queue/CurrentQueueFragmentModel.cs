// <copyright file="CurrentQueueFragmentModel.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using Android.Media.Browse;

namespace Apollo
{
    /// <summary>
    /// Model class for the <see cref="CurrentQueueFragmentPresenter"/>.
    /// </summary>
    internal class CurrentQueueFragmentModel
    {
        private readonly MusicBrowser musicBrowser;

        /// <summary>
        /// Initializes a new instance of the <see cref="CurrentQueueFragmentModel"/> class.
        /// </summary>
        /// <param name="musicBrowser">Connected instance of a <see cref="MusicBrowser"/>.</param>
        public CurrentQueueFragmentModel(MusicBrowser musicBrowser)
        {
            this.musicBrowser = musicBrowser;
        }

        /// <summary>
        /// Returns the media ID for the active queue item when given a queue ID.
        /// </summary>
        /// <param name="queueId"><see cref="int"/> queue ID.</param>
        /// <returns>Media ID.</returns>
        public string GetQueueItemMediaId(long queueId)
        {
            if (musicBrowser.Queue != null)
            {
                foreach (var queueItem in musicBrowser.Queue)
                {
                    if (queueItem.QueueId == queueId)
                        return queueItem.Description.MediaId;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the queue index position of an item by queue ID.
        /// </summary>
        /// <param name="queueId">The queue ID of the item.</param>
        /// <returns>The integer queue position of the item.</returns>
        public int GetQueueItemPosition(long queueId)
        {
            if (musicBrowser.Queue != null)
            {
                var index = 0;
                foreach (var queueItem in musicBrowser.Queue)
                {
                    if (queueItem.QueueId == queueId)
                        return index;
                    else
                        index++;
                }
            }

            return -1;
        }
    }
}