using Microsoft.Maui.Controls;
using MeshNoteLM.ViewModels;

namespace MeshNoteLM.Controls
{
    public enum ThreePaneMode
    {
        Sources, View, Chat
    }

    [Flags]
    public enum PanelVisibility
    {
        None = 0,
        Sources = 1,
        View = 2,
        Chat = 4
    }

    public partial class ThreePaneLayout : Grid
    {
        public static readonly BindableProperty PaneAProperty =
            BindableProperty.Create(nameof(PaneA), typeof(View), typeof(ThreePaneLayout), propertyChanged: OnPaneChanged);

        public static readonly BindableProperty PaneBProperty =
            BindableProperty.Create(nameof(PaneB), typeof(View), typeof(ThreePaneLayout), propertyChanged: OnPaneChanged);

        public static readonly BindableProperty PaneCProperty =
            BindableProperty.Create(nameof(PaneC), typeof(View), typeof(ThreePaneLayout), propertyChanged: OnPaneChanged);

        public static readonly BindableProperty VisiblePanelsProperty =
            BindableProperty.Create(nameof(VisiblePanels), typeof(PanelVisibility), typeof(ThreePaneLayout),
                PanelVisibility.Sources | PanelVisibility.View, propertyChanged: OnVisiblePanelsChanged);

        public View PaneA
        {
            get => (View)GetValue(PaneAProperty);
            set => SetValue(PaneAProperty, value);
        }

        public View PaneB
        {
            get => (View)GetValue(PaneBProperty);
            set => SetValue(PaneBProperty, value);
        }

        public View PaneC
        {
            get => (View)GetValue(PaneCProperty);
            set => SetValue(PaneCProperty, value);
        }

        // Add FadeTo method for compatibility
        public void FadeTo(double opacity)
        {
            // Simple opacity fade without animation for now
            this.Opacity = opacity;
        }

        public void ApplyLayout(bool animated = false)
        {
            // Clear all children and reset
            Children.Clear();

            // Calculate which panels should be visible and their columns
            var visiblePanels = new List<(int column, View pane)>();
            if (IsSourcesVisible && PaneA is not null)
                visiblePanels.Add((0, PaneA));
            if (IsViewVisible && PaneB is not null)
                visiblePanels.Add((1, PaneB));
            if (IsChatVisible && PaneC is not null)
                visiblePanels.Add((2, PaneC));

            // If no panels are visible, show Sources and View by default
            if (visiblePanels.Count == 0)
            {
                if (PaneA is not null)
                    visiblePanels.Add((0, PaneA));
                if (PaneB is not null)
                    visiblePanels.Add((1, PaneB));
                VisiblePanels = PanelVisibility.Sources | PanelVisibility.View;
            }

            // Set column definitions based on visible count
            ColumnDefinitions.Clear();
            if (visiblePanels.Count == 1)
            {
                // Single panel takes full width
                ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            }
            else
            {
                // Multiple panels share space equally
                for (int i = 0; i < visiblePanels.Count; i++)
                {
                    ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
                }
            }

            // Set up Grid based on visible panels
            for (int i = 0; i < visiblePanels.Count; i++)
            {
                var (column, pane) = visiblePanels[i];

                // Set Grid.SetRow and Grid.SetColumn for the pane
                Grid.SetRow(pane, 0);
                Grid.SetColumn(pane, column);

                // Add to Children
                Children.Add(pane);
            }

            // Animate if requested
            if (animated)
            {
                this.FadeTo(0.5);
                this.FadeTo(1.0);
            }
        }

        public PanelVisibility VisiblePanels
        {
            get => (PanelVisibility)GetValue(VisiblePanelsProperty);
            set => SetValue(VisiblePanelsProperty, value);
        }

        public bool IsSourcesVisible => VisiblePanels.HasFlag(PanelVisibility.Sources);
        public bool IsViewVisible => VisiblePanels.HasFlag(PanelVisibility.View);
        public bool IsChatVisible => VisiblePanels.HasFlag(PanelVisibility.Chat);

        public ThreePaneLayout()
        {
            RowDefinitions.Clear();
            ColumnDefinitions.Clear();

            // Create a single row layout
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

            // Don't apply layout here - wait for panes to be assigned and visibility to be set
        }

        // Public methods to toggle individual panels
        public void ToggleSources()
        {
            VisiblePanels ^= PanelVisibility.Sources;
        }

        public void ToggleView()
        {
            VisiblePanels ^= PanelVisibility.View;
        }

        public void ToggleChat()
        {
            VisiblePanels ^= PanelVisibility.Chat;
        }

        public void ShowOnlySources()
        {
            VisiblePanels = PanelVisibility.Sources;
        }

        public void ShowOnlyView()
        {
            VisiblePanels = PanelVisibility.View;
        }

        public void ShowOnlyChat()
        {
            VisiblePanels = PanelVisibility.Chat;
        }

        public void ShowAll()
        {
            VisiblePanels = PanelVisibility.Sources | PanelVisibility.View | PanelVisibility.Chat;
        }

        public void HideAll()
        {
            VisiblePanels = PanelVisibility.None;
        }

        private static void OnPaneChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is ThreePaneLayout tpl)
            {
                tpl.RebuildChildren();
                tpl.ApplyLayout();
            }
        }

        private static void OnVisiblePanelsChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is ThreePaneLayout tpl && newValue is PanelVisibility)
                tpl.ApplyLayout(animated: true);
        }

        private void RebuildChildren()
        {
            // Just clear children - ApplyLayout will handle proper repositioning
            Children.Clear();
        }
    }
}
