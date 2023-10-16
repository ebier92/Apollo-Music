// <copyright file="IHomeFragment.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Threading;
using Android.OS;
using Android.Views;

namespace Apollo
{
    internal interface IHomeFragment
    {
        CancellationToken CancellationToken { get; }

        void AddHomeContentItem(YouTube.ContentItem homeContentItem);

        void DisplayConfirmationPrompt(int messageStringId, Action positiveButtonAction);

        void DisplaySettingsPrompt();

        void NotifyNetworkLost();

        View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState);

        void UpdateHomeContentItemsLoadingIconVisibility(ViewStates viewState);

        void UpdateRetryHomeContentItemsLoadItemsVisibility(ViewStates viewState);
    }
}