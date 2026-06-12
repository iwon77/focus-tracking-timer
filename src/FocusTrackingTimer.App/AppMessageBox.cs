using System.Windows;

namespace FocusTrackingTimer.App;

internal static class AppMessageBox
{
    public static MessageBoxResult Show(
        string messageBoxText,
        string caption,
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None)
    {
        return ShowCore(owner: null, messageBoxText, caption, button, icon, ThemedMessageDialog.AppMessageBoxLayout.Themed);
    }

    public static MessageBoxResult Show(
        Window owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return ShowCore(owner, messageBoxText, caption, button, icon, ThemedMessageDialog.AppMessageBoxLayout.Themed);
    }

    public static MessageBoxResult ShowPlain(
        Window owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return ShowCore(owner, messageBoxText, caption, button, icon, ThemedMessageDialog.AppMessageBoxLayout.Plain);
    }

    private static MessageBoxResult ShowCore(
        Window? owner,
        string messageBoxText,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        ThemedMessageDialog.AppMessageBoxLayout layout)
    {
        ThemedMessageDialog dialog = new(
            caption,
            messageBoxText,
            button,
            icon,
            layout);

        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        return dialog.ShowModal();
    }
}
