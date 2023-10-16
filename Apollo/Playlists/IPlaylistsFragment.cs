// <copyright file="IPlaylistsFragment.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Android.Media.Browse;
using Android.OS;
using Android.Views;

namespace Apollo
{
    internal interface IPlaylistsFragment
    {
        View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState);

        void OnHiddenChanged(bool hidden);

        void OnStart();

        void UpdateEmptyPageElementsVisibility(ViewStates viewState);

        void UpdatePlaylistItems(IList<MediaBrowser.MediaItem> playlistItems);

        void UpdateRecyclerViewVisibility(ViewStates viewState);
    }
}