// <copyright file="ICurrentQueueFragment.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Android.Media.Session;
using Android.OS;
using Android.Views;
using AndroidX.RecyclerView.Widget;

namespace Apollo
{
    internal interface ICurrentQueueFragment
    {
        bool AdapterInitialized { get; }

        bool FilteredItemSelected { get; set; }

        bool FilterEnabled { get; }

        bool ItemTouchHelperInitialized { get; }

        void DisplayDeleteQueueItemsConfirmation(string playlistName);

        void DisplayErrorMessage(string message);

        void DisplayOverwriteQueueItemsConfirmation(string playlistName);

        void DisplayPromptToSaveQueueItems(string playlistName);

        void InitializeActiveQueueItem(string activeItemMediaId, int position);

        void InitializeCurrentQueueAdapter(IList<MediaSession.QueueItem> queueItems);

        void InitializeItemTouchHelper();

        void NotifyFilterEnabledChange();

        void NotifyGeneratePlaylistComplete(bool initialPlaylist);

        void NotifyPlaybackStateChange(PlaybackState playbackState);

        void NotifySaveComplete();

        void NotifyShuffle();

        View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState);

        void OnDestroy();

        void OnHiddenChanged(bool hidden);

        void OnStartDrag(RecyclerView.ViewHolder viewHolder);

        void ScrollToPosition(int position);

        void UpdateActiveQueueItem(string newActiveItemMediaId);

        void UpdateCurrentQueueBackgroundVisibility(ViewStates viewState);

        void UpdateEmptyQueueMessageVisibility(ViewStates viewState);

        void UpdateMainComponentVisibility(ViewStates viewState);

        void UpdatePlaylistGeneratingProgressBarVisibility(ViewStates viewState);

        void UpdatePlaylistLoadingProgressBarVisibility(ViewStates viewState);

        void UpdatePlaylistName(string playlistName);

        void UpdateQueueItems(IList<MediaSession.QueueItem> queueItems);

        void UpdateSearchViewQuery(string queryText);

        void UpdateShuffleButton(bool shuffleMode);
    }
}