using Microsoft.Maui.Controls;
using YourApp.Controls;

namespace YourApp.Pages
{
    public partial class ThreePanePage : ContentPage
    {
        public ThreePanePage()
        {
            InitializeComponent();
            PaneHost.Mode = ThreePaneMode.AB; // default
            // Optional: gestures — double-tap a pane to maximize it
            AddMaximizeGestures();
        }

        void OnAFull(object sender, EventArgs e) => PaneHost.Mode = ThreePaneMode.AFull;
        void OnBFull(object sender, EventArgs e) => PaneHost.Mode = ThreePaneMode.BFull;
        void OnCFull(object sender, EventArgs e) => PaneHost.Mode = ThreePaneMode.CFull;
        void OnAB(object sender, EventArgs e)    => PaneHost.Mode = ThreePaneMode.AB;
        void OnAC(object sender, EventArgs e)    => PaneHost.Mode = ThreePaneMode.AC;
        void OnBC(object sender, EventArgs e)    => PaneHost.Mode = ThreePaneMode.BC;

        void AddMaximizeGestures()
        {
            PaneHost.PaneA?.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    NumberOfTapsRequired = 2,
                    Command = new Command(() => PaneHost.Mode = ThreePaneMode.AFull)
                });

            PaneHost.PaneB?.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    NumberOfTapsRequired = 2,
                    Command = new Command(() => PaneHost.Mode = ThreePaneMode.BFull)
                });

            PaneHost.PaneC?.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    NumberOfTapsRequired = 2,
                    Command = new Command(() => PaneHost.Mode = ThreePaneMode.CFull)
                });
        }
    }
}
