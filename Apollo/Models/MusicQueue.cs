// <copyright file="MusicQueue.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Android.Media.Session;
using Java.Util;

namespace Apollo
{
    /// <summary>
    /// Manages the music queue for the <see cref="MusicService"/> class.
    /// </summary>
    internal class MusicQueue
    {
        private List<MediaSession.QueueItem> queue;
        private List<MediaSession.QueueItem> unshuffledQueue;

        /// <summary>
        /// Initializes a new instance of the <see cref="MusicQueue"/> class.
        /// </summary>
        public MusicQueue()
        {
            queue = new List<MediaSession.QueueItem>();
            unshuffledQueue = new List<MediaSession.QueueItem>();
            QueueIndex = 0;
        }

        public int QueueIndex { get; private set; }

        public List<MediaSession.QueueItem> Queue
        {
            get
            {
                return queue;
            }

            set
            {
                queue = value;
                QueueIndex = 0;
            }
        }

        public int QueueLength
        {
            get { return Queue.Count; }
        }

        /// <summary>
        /// Performs a shuffle operation on an outside list of queue items so shuffles can be performed before queue items are set.
        /// </summary>
        /// <param name="queueItems">The queue item list to shuffle.</param>
        /// <param name="doNotShuffleFirstItem">A flag to leave the current first track at the beginning of the shuffled queue items list when set to true.</param>
        public static void Shuffle(List<MediaSession.QueueItem> queueItems, bool doNotShuffleFirstItem)
        {
            // Exit if queue count is 0
            if (queueItems.Count <= 1)
                return;

            // Exit if queue count is 1 or less if do not shuffle first item flag is enabled
            if (queueItems.Count <= 1 && doNotShuffleFirstItem)
                return;

            // Save the first queue item
            var firstQueueItem = queueItems[0];

            // If the do not shuffle first item flag is set, remove the current queue item so it can be placed in the front of the queue later
            if (doNotShuffleFirstItem)
                queueItems.Remove(firstQueueItem);

            // Set flag to indicate when at least one item is moved to a different position meaning the shuffle was successful
            var shuffleSuccess = false;

            while (!shuffleSuccess)
            {
                // Shuffle using Fisher-Yates
                var random = new Random();
                var n = queueItems.Count;

                while (n > 1)
                {
                    n--;
                    var k = random.NextInt(n + 1);

                    // Set shuffle success flag
                    if (!shuffleSuccess && n != k)
                        shuffleSuccess = true;

                    // Swap queue item order
                    var queueItem = queueItems[k];
                    queueItems[k] = queueItems[n];
                    queueItems[n] = queueItem;
                }
            }

            // If the do not shuffle first item flag is set, add the current queue item back as the first item and set index to 0
            if (doNotShuffleFirstItem)
            {
                queueItems.Insert(0, firstQueueItem);
            }
        }

        /// <summary>
        /// Increments the index of the current queue item by 1. Rolls the index back to 0 if the increment moves the index higher than the number of tracks in the queue.
        /// </summary>
        public void IncrementIndex()
        {
            if (QueueIndex >= QueueLength)
                QueueIndex = 0;
            else
                QueueIndex++;
        }

        /// <summary>
        /// Decrements the index of the current queue item by 1. Rolls the index back to the end of the queue if the decrement moves the index below 0.
        /// </summary>
        public void DecrementIndex()
        {
            if (QueueIndex < 0)
                QueueIndex = Queue.Count - 1;
            else
                QueueIndex--;
        }

        /// <summary>
        /// Retrieves the <see cref="MediaSession.QueueItem"/> at the current index.
        /// </summary>
        /// <returns><see cref="MediaSession.QueueItem"/>.</returns>
        public MediaSession.QueueItem GetCurrentItem()
        {
            // If the current queue index is valid, return the queue item at the index, else return null
            if (QueueIndex >= 0 && QueueIndex <= QueueLength - 1)
                return queue[QueueIndex];
            else
                return null;
        }

        /// <summary>
        /// Retrieves a <see cref="MediaSession.QueueItem"/> by an integer index.
        /// </summary>
        /// <param name="index">Integer index value.</param>
        /// <returns><see cref="MediaSession.QueueItem"/> at the index in the queue.</returns>
        public MediaSession.QueueItem GetItem(int index)
        {
            // If the index is valid, return the queue item at the index, else return null
            if (index >= 0 && index < QueueLength)
                return queue[index];
            else
                return null;
        }

        /// <summary>
        /// Retrieves a <see cref="MediaSession.QueueItem"/> by matching media ID.
        /// </summary>
        /// <param name="mediaId">String media ID.</param>
        /// <returns><see cref="MediaSession.QueueItem"/> matching the media ID.</returns>
        public MediaSession.QueueItem GetItem(string mediaId)
        {
            int index = 0;

            foreach (var item in queue)
            {
                if (mediaId == item.Description.MediaId)
                    return queue[index];
                else
                    index++;
            }

            return null;
        }

        /// <summary>
        /// Updates the queue index to point at the <see cref="MediaSession.QueueItem"/> matching the queue ID.
        /// </summary>
        /// <param name="queueId">Long queue ID.</param>
        public void SetItemByQueueId(long queueId)
        {
            int index = 0;

            foreach (var item in Queue)
            {
                if (queueId == item.QueueId)
                {
                    QueueIndex = index;
                    break;
                }
                else
                {
                    index++;
                }
            }
        }

        /// <summary>
        /// Moves an item in the music queue to a specified position by media ID.
        /// </summary>
        /// <param name="mediaId">The media ID of the queue item to move.</param>
        /// <param name="toPosition">The integer position in the queue to move the item to.</param>
        public void MoveItem(string mediaId, int toPosition)
        {
            // Save the current queue item pointed at by the index for later reference
            var currentQueueItem = GetCurrentItem();

            // Get the queue item by media ID
            var item = GetItem(mediaId);

            // Move the item if the media ID and target movement position are valid
            if (item != null && toPosition >= 0 && toPosition <= QueueLength - 1 && toPosition != queue.IndexOf(item))
            {
                queue.Remove(item);
                queue.Insert(toPosition, item);
            }

            // Set the queue index back to the current queue item in case the move has affected the item at the index
            QueueIndex = Queue.IndexOf(currentQueueItem);
        }

        /// <summary>
        /// Removes the music queue item at the specified position.
        /// </summary>
        /// <param name="position">The queue position to remove an item from.</param>
        public void RemoveItem(int position)
        {
            // If the removal position is not equal to the queue index, save the current queue item for later reference
            MediaSession.QueueItem currentQueueItem = null;

            if (position != QueueIndex)
                currentQueueItem = GetCurrentItem();

            // Remove the item at the position if the position is valid
            if (position >= 0 && position <= QueueLength - 1)
                queue.RemoveAt(position);

            // Set the queue index back to the current queue item if this was not the item that was removed
            if (position != QueueIndex)
                QueueIndex = Queue.IndexOf(currentQueueItem);
        }

        /// <summary>
        /// Inserts a queue item at the next position in the queue.
        /// </summary>
        /// <param name="queueItem">The <see cref="MediaSession.QueueItem"/> to insert.</param>
        public void InsertNext(MediaSession.QueueItem queueItem)
        {
            Queue.Insert(QueueIndex + 1, queueItem);

            // Add to the end of the unshuffled queue if in shuffle mode
            if (unshuffledQueue != null)
                unshuffledQueue.Add(queueItem);
        }

        /// <summary>
        /// Inserts a queue item at the end of the queue.
        /// </summary>
        /// <param name="queueItem">The <see cref="MediaSession.QueueItem"/> to insert.</param>
        public void InsertLast(MediaSession.QueueItem queueItem)
        {
            Queue.Add(queueItem);

            // Add to the end of the unshuffled queue if in shuffle mode
            if (unshuffledQueue != null)
                unshuffledQueue.Add(queueItem);
        }

        /// <summary>
        /// Shuffles the current music queue in a random order.
        /// </summary>
        /// <param name="moveCurrentQueueItemToFirst">Flag that if set to true will move the current queue item at the index to the front of the queue after shuffling.</param>
        public void Shuffle(bool moveCurrentQueueItemToFirst)
        {
            // Exit if queue count is 0
            if (queue.Count <= 1)
                return;

            // Exit if queue count is 1 or less if do not shuffle first item flag is enabled
            if (queue.Count <= 1 && moveCurrentQueueItemToFirst)
                return;

            // Save the current queue item to restore the queue index
            var currentQueueItem = GetCurrentItem();

            // Backup the queue to the unshuffled queue
            unshuffledQueue = new List<MediaSession.QueueItem>(queue);

            // If the move current queue item to first flag is set, remove the current queue item so it can be placed in the front of the queue later
            if (moveCurrentQueueItemToFirst)
                queue.Remove(currentQueueItem);

            // Set flag to indicate when at least one item is moved to a different position meaning the shuffle was successful
            var shuffleSuccess = false;

            while (!shuffleSuccess)
            {
                // Shuffle using Fisher-Yates
                var random = new Random();
                var n = queue.Count;

                while (n > 1)
                {
                    n--;
                    var k = random.NextInt(n + 1);

                    // Set shuffle success flag
                    if (!shuffleSuccess && n != k)
                        shuffleSuccess = true;

                    // Swap queue item order
                    var queueItem = queue[k];
                    queue[k] = queue[n];
                    queue[n] = queueItem;
                }
            }

            // If the flag to move the current queue item to first is set, add the current queue item back as the first item and set index to 0
            if (moveCurrentQueueItemToFirst)
            {
                queue.Insert(0, currentQueueItem);
                QueueIndex = 0;
            } // If the flag is not set, set the index back to the index of the item
            else
            {
                QueueIndex = Queue.IndexOf(currentQueueItem);
            }
        }

        /// <summary>
        /// Restores the current music queue to its unshuffled order.
        /// </summary>
        public void Unshuffle()
        {
            if (unshuffledQueue.Count > 0)
            {
                // Save the current queue item to restore the queue index
                var currentQueueItemId = GetCurrentItem().QueueId;

                // Set the queue back to the unshuffled queue and reinitialize the unshuffled queue
                queue = unshuffledQueue;
                unshuffledQueue = new List<MediaSession.QueueItem>();

                // Set the queue index back to the current queue item
                SetItemByQueueId(currentQueueItemId);
            }
        }
    }
}