// <copyright file="PlayerFragment.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using Android.OS;
using Android.Views;
using Android.Widget;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace Apollo
{
    /// <summary>
    /// <see cref="Fragment"/> class to display the music player.
    /// </summary>
    internal class PlayerFragment : Fragment
    {
        private MainActivity mainActivity;
        private LinearLayout playerBackground;

        /// <summary>
        /// Inflates the <see cref="PlayerFragment"/>.
        /// </summary>
        /// <param name="inflater">A <see cref="LayoutInflater"/> to inflate the view.</param>
        /// <param name="container">A containing <see cref="ViewGroup"/>.</param>
        /// <param name="savedInstanceState">A <see cref="Bundle"/> representing the previousy saved state.</param>
        /// <returns>Inflated <see cref="View"/>.</returns>
        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            mainActivity = (MainActivity)Activity;

            var view = inflater.Inflate(Resource.Layout.fragment_player, container, false);

            playerBackground = view.FindViewById<LinearLayout>(Resource.Id.player_content_container);

            playerBackground.Click += (sender, e) =>
            {
                mainActivity.OnPanelClicked();
            };

            return view;
        }
    }
}