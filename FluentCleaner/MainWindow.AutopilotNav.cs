using FluentCleaner.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace FluentCleaner;

public sealed partial class MainWindow
{
    private void NavView_SelectionChangedWithAutopilot(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        TitleSearchBox.Text = "";
        var transition = new DrillInNavigationTransitionInfo();

        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage), null, transition);
        }
        else if (args.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag)
            {
                case "Cleaner":
                    NavFrame.Navigate(typeof(CleanerPage), null, transition);
                    break;
                case "Autopilot":
                    NavFrame.Navigate(typeof(AutopilotPage), null, transition);
                    break;
                case "Terminal":
                    NavFrame.Navigate(typeof(TerminalPage), null, transition);
                    break;
                case "Custom":
                    NavFrame.Navigate(typeof(CustomPage), null, transition);
                    break;
            }
        }

        TitleSearchBox.IsEnabled = NavFrame.Content is ISearchablePage;
        SearchIconButton.IsEnabled = NavFrame.Content is ISearchablePage;
    }
}
