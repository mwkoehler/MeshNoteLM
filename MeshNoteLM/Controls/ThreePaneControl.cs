using System;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MeshNoteLM.Controls
{
    public enum PaneMode { Full, Half, Minimized }

    public partial class ThreePaneControl : Grid
    {
        // ----- Hosts that live in the Grid exactly once -----
        private readonly ContentView _pane1Host = new() { BackgroundColor = Colors.Transparent, Padding = 0 };
        private readonly ContentView _pane2Host = new() { BackgroundColor = Colors.Transparent, Padding = 0 };
        private readonly ContentView _pane3Host = new() { BackgroundColor = Colors.Transparent, Padding = 0 };

        private readonly BoxView _splitter1 = new() { WidthRequest = 6, BackgroundColor = Colors.Gray, Opacity = 0.3, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
        private readonly BoxView _splitter2 = new() { WidthRequest = 6, BackgroundColor = Colors.Gray, Opacity = 0.3, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };

        // Star “weights” for the three resizable columns
        private double _pane1Star = 1;
        private double _pane2Star = 1;
        private double _pane3Star = 1;

        // ----- Bindable Properties (pane content) -----
        public static readonly BindableProperty Pane1Property =
            BindableProperty.Create(
                nameof(Pane1),
                typeof(View),
                typeof(ThreePaneControl),
                default(View),
                propertyChanged: (b, o, n) => ((ThreePaneControl)b)._pane1Host.Content = (View?)n
            );

        public static readonly BindableProperty Pane2Property =
            BindableProperty.Create(
                nameof(Pane2),
                typeof(View),
                typeof(ThreePaneControl),
                default(View),
                propertyChanged: (b, o, n) => ((ThreePaneControl)b)._pane2Host.Content = (View?)n
            );

        public static readonly BindableProperty Pane3Property =
            BindableProperty.Create(
                nameof(Pane3),
                typeof(View),
                typeof(ThreePaneControl),
                default(View),
                propertyChanged: (b, o, n) => ((ThreePaneControl)b)._pane3Host.Content = (View?)n
            );

        public View? Pane1 { get => (View?)GetValue(Pane1Property); set => SetValue(Pane1Property, value); }
        public View? Pane2 { get => (View?)GetValue(Pane2Property); set => SetValue(Pane2Property, value); }
        public View? Pane3 { get => (View?)GetValue(Pane3Property); set => SetValue(Pane3Property, value); }

        public ThreePaneControl()
        {
            BuildLayout();
            WireSplitters();
        }

        private void BuildLayout()
        {
            RowDefinitions.Clear();
            ColumnDefinitions.Clear();
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

            // [0]=pane1  [1]=splitter1  [2]=pane2  [3]=splitter2  [4]=pane3
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_pane1Star, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_pane2Star, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_pane3Star, GridUnitType.Star) });

            // Add hosts once
            Grid.SetRow((BindableObject)_pane1Host, 0);
            Grid.SetColumn((BindableObject)_pane1Host, 0);
            Children.Add(_pane1Host);

            Grid.SetRow((BindableObject)_pane2Host, 0);
            Grid.SetColumn((BindableObject)_pane2Host, 2);
            Children.Add(_pane2Host);

            Grid.SetRow((BindableObject)_pane3Host, 0);
            Grid.SetColumn((BindableObject)_pane3Host, 4);
            Children.Add(_pane3Host);

            // Add splitters
            Grid.SetRow((BindableObject)_splitter1, 0);
            Grid.SetColumn((BindableObject)_splitter1, 1);
            Children.Add(_splitter1);

            Grid.SetRow((BindableObject)_splitter2, 0);
            Grid.SetColumn((BindableObject)_splitter2, 3);
            Children.Add(_splitter2);
        }

        private void WireSplitters()
        {
            var pan1 = new PanGestureRecognizer();
            pan1.PanUpdated += (s, e) =>
            {
                if (e.StatusType == GestureStatus.Running)
                    AdjustBetween(ref _pane1Star, ref _pane2Star, e.TotalX);
            };
            _splitter1.GestureRecognizers.Add(pan1);

            var pan2 = new PanGestureRecognizer();
            pan2.PanUpdated += (s, e) =>
            {
                if (e.StatusType == GestureStatus.Running)
                    AdjustBetween(ref _pane2Star, ref _pane3Star, e.TotalX);
            };
            _splitter2.GestureRecognizers.Add(pan2);
        }

        private void AdjustBetween(ref double leftStar, ref double rightStar, double deltaPixels)
        {
            // Convert pixel delta to “star” delta relative to current width.
            // Keep it simple: use control width to estimate scale.
            var width = Math.Max(1, Width);
            var totalStar = Math.Max(0.0001, leftStar + rightStar);

            // Map pixels to a small star change.
            var starDelta = (deltaPixels / width) * totalStar * 3; // multiplier = sensitivity tweak

            var newLeft = Math.Max(0.1, leftStar + starDelta);
            var newRight = Math.Max(0.1, rightStar - starDelta);

            leftStar = newLeft;
            rightStar = newRight;

            ColumnDefinitions[0].Width = new GridLength(_pane1Star, GridUnitType.Star);
            ColumnDefinitions[2].Width = new GridLength(_pane2Star, GridUnitType.Star);
            ColumnDefinitions[4].Width = new GridLength(_pane3Star, GridUnitType.Star);
        }

        /// <summary>
        /// Optional helper to set pane sizes programmatically.
        /// </summary>
        public void SetPaneModes(PaneMode pane1, PaneMode pane2, PaneMode pane3)
        {
            static double ToStar(PaneMode m) => m switch
            {
                PaneMode.Full => 2,
                PaneMode.Half => 1,
                PaneMode.Minimized => 0.1, // still visible
                _ => 1
            };

            _pane1Star = ToStar(pane1);
            _pane2Star = ToStar(pane2);
            _pane3Star = ToStar(pane3);

            ColumnDefinitions[0].Width = new GridLength(_pane1Star, GridUnitType.Star);
            ColumnDefinitions[2].Width = new GridLength(_pane2Star, GridUnitType.Star);
            ColumnDefinitions[4].Width = new GridLength(_pane3Star, GridUnitType.Star);

            // If you want to actually hide minimized panes, you could also toggle IsVisible here.
        }
    }
}
